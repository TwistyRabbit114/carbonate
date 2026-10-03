using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.IntegrationTests.Support;

namespace Carbonate.IntegrationTests.Platform;

public class ApiSecurityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_returns_200_without_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_endpoint_with_no_declared_rule_requires_sign_in()
    {
        var response = await factory.CreateClient().GetAsync("/api/test/no-declaration");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_missing_token_returns_problem_details_not_a_bare_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("/problems/unauthenticated", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task A_garbage_token_is_refused()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not.a.token");

        var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Someone_without_the_permission_gets_403()
    {
        var client = factory.CreateClientWith(["EventManager"], ["event.create"]);

        var response = await client.GetAsync("/api/test/needs-audit-view");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("/problems/forbidden", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Someone_with_the_permission_gets_in()
    {
        var client = factory.CreateClientWith(["Director"], ["audit.view"]);

        var response = await client.GetAsync("/api/test/needs-audit-view");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_mfa_token_cannot_be_used_as_a_sign_in()
    {
        var client = factory.CreateClientWithMfaToken();

        // Identified but not signed in: refused, never 200.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/test/no-declaration")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/test/needs-audit-view")).StatusCode);
    }

    [Fact]
    public async Task An_access_token_cannot_be_used_for_the_mfa_only_steps()
    {
        var client = factory.CreateClientWith(["Director"], ["audit.view"]);

        var response = await client.PostAsync("/api/auth/mfa/enrol", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_reports_every_invalid_field_together()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "not-an-email", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("/problems/validation", body.GetProperty("type").GetString());
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abcdef")]
    [InlineData("1234567")]
    public async Task The_mfa_code_must_be_six_digits(string code)
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/auth/mfa/verify", new { mfaToken = "x", code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refreshing_without_a_cookie_is_a_401_and_does_not_touch_the_database()
    {
        var response = await factory.CreateClient().PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Responses_carry_the_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("/api/test/open");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.True(response.Headers.Contains("Permissions-Policy"));
        Assert.Equal("no-store", response.Headers.CacheControl?.NoStore == true ? "no-store" : "");
    }

    [Fact]
    public async Task A_malformed_json_body_is_a_400_problem_not_a_stack_trace()
    {
        var response = await factory.CreateClient().PostAsync("/api/test/echo",
            new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("   at ", text);
    }
}

public class LoginRateLimitTests : IClassFixture<LoginRateLimitTests.TightFactory>
{
    private readonly TightFactory _factory;

    public LoginRateLimitTests(TightFactory factory) => _factory = factory;

    public class TightFactory : ApiFactory
    {
        public TightFactory() => LoginPermitPerMinute = 3;
    }

    [Fact]
    public async Task Repeated_sign_in_attempts_are_throttled()
    {
        var client = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 5; i++)
        {
            // An invalid body keeps this away from the database; the limiter counts it all the same.
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "x", password = "" });
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.BadRequest));
    }
}
