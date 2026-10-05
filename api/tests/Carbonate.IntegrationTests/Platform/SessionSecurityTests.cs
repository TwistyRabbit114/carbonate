using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Carbonate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carbonate.IntegrationTests.Platform;

/// <summary>
/// The security behaviour that only shows over HTTP with a real database: lockout, the single-use refresh
/// cookie and what happens when it is replayed, forged tokens, and hostile text stored in free-text fields
/// (FR-34, NFR-16 to NFR-20). Runs in its own database, with the sign-in throttle raised so the throttle
/// itself does not interfere (it has its own test).
/// </summary>
public class SessionSecurityTests(DatabaseApiFixture fixture) : IClassFixture<DatabaseApiFixture>
{
    private const string Password = "Correct-Horse-Battery-9!";
    private const string RefreshCookie = "carbonate.refresh";

    private readonly WebApplicationFactory<Program> _factory =
        fixture.Factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:LoginPermitPerMinute", "1000"));

    private async Task<AppUser> UserWithPasswordAsync(string role = RoleNames.EventManager)
    {
        var user = await new Scenario(fixture.Factory).UserAsync(role);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Carbonate.Infrastructure.Persistence.CemDbContext>();
        var tracked = await db.Users.SingleAsync(u => u.UserId == user.UserId);
        tracked.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(tracked, Password);
        await db.SaveChangesAsync();
        return tracked;
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

    private async Task<HttpResponseMessage> RefreshAsync(string cookieValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"{RefreshCookie}={cookieValue}");
        return await _factory.CreateClient().SendAsync(request);
    }

    private static string CookieValue(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").First(h => h.StartsWith(RefreshCookie + "=", StringComparison.Ordinal));
        return header[(RefreshCookie.Length + 1)..].Split(';')[0];
    }

    // ---- lockout ---------------------------------------------------------------------------------

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_against_the_right_password()
    {
        var user = await UserWithPasswordAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(user.Email, "wrong-password-" + attempt)).StatusCode);
        }

        var locked = await LoginAsync(user.Email, Password);
        Assert.NotEqual(HttpStatusCode.OK, locked.StatusCode);

        var lockoutEnd = await new Scenario(fixture.Factory).WithDbAsync(db =>
            db.Users.Where(u => u.UserId == user.UserId).Select(u => u.LockoutEnd).SingleAsync());
        Assert.NotNull(lockoutEnd);
        Assert.True(lockoutEnd > DateTime.UtcNow.AddMinutes(10), "The lockout should last about 15 minutes.");
    }

    [Fact]
    public async Task The_account_signs_in_again_once_the_lockout_has_passed()
    {
        var user = await UserWithPasswordAsync();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(user.Email, "wrong-password-" + attempt);
        }

        await new Scenario(fixture.Factory).WithDbAsync(async db =>
        {
            var tracked = await db.Users.SingleAsync(u => u.UserId == user.UserId);
            tracked.LockoutEnd = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(user.Email, Password)).StatusCode);
    }

    [Fact]
    public async Task An_unknown_email_and_a_wrong_password_look_the_same()
    {
        var user = await UserWithPasswordAsync();

        var unknown = await LoginAsync("nobody@example.test", Password);
        var wrong = await LoginAsync(user.Email, "not-the-password-1");

        Assert.Equal(unknown.StatusCode, wrong.StatusCode);
        var unknownBody = await unknown.Content.ReadFromJsonAsync<JsonElement>();
        var wrongBody = await wrong.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(unknownBody.GetProperty("title").GetString(), wrongBody.GetProperty("title").GetString());
        Assert.Equal(unknownBody.GetProperty("detail").GetString(), wrongBody.GetProperty("detail").GetString());
    }

    // ---- refresh cookie --------------------------------------------------------------------------

    [Fact]
    public async Task The_refresh_cookie_is_http_only_strict_and_scoped_to_the_auth_routes()
    {
        var user = await UserWithPasswordAsync();

        var login = await LoginAsync(user.Email, Password);

        var header = login.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith(RefreshCookie + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", await login.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_refresh_cookie_works_once_and_a_replay_revokes_the_whole_chain()
    {
        var user = await UserWithPasswordAsync();
        var first = CookieValue(await LoginAsync(user.Email, Password));

        var rotated = await RefreshAsync(first);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var second = CookieValue(rotated);
        Assert.NotEqual(first, second);

        // Someone replays the cookie that has already been used: refused, and the newer cookie dies too.
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(second)).StatusCode);
    }

    [Fact]
    public async Task A_refresh_cookie_stops_working_when_the_account_is_deactivated()
    {
        var user = await UserWithPasswordAsync();
        var cookie = CookieValue(await LoginAsync(user.Email, Password));

        await new Scenario(fixture.Factory).WithDbAsync(async db =>
        {
            var tracked = await db.Users.SingleAsync(u => u.UserId == user.UserId);
            tracked.IsActive = false;
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(cookie)).StatusCode);
    }

    [Fact]
    public async Task A_made_up_refresh_cookie_is_refused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(Convert.ToBase64String(new byte[32]))).StatusCode);
    }

    // ---- forged access tokens --------------------------------------------------------------------

    [Fact]
    public async Task A_token_with_extra_permissions_added_but_not_re_signed_is_refused()
    {
        var user = await UserWithPasswordAsync(RoleNames.CasualCrew);
        var session = await (await LoginAsync(user.Email, Password)).Content.ReadFromJsonAsync<JsonElement>();
        var token = session.GetProperty("accessToken").GetString()!;
        var parts = token.Split('.');

        var payload = JsonDocument.Parse(Base64UrlDecode(parts[1])).RootElement;
        var forged = new Dictionary<string, object?>();
        foreach (var property in payload.EnumerateObject())
        {
            forged[property.Name] = property.Value.Clone();
        }

        forged["perm"] = new[] { PermissionCodes.UserManage, PermissionCodes.FinanceViewMargin };
        forged["permission"] = forged["perm"];
        var tampered = $"{parts[0]}.{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(forged))}.{parts[2]}";

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task A_token_with_the_signature_removed_is_refused()
    {
        var (_, director) = await new Scenario(fixture.Factory).SignedInAsync(RoleNames.Director);
        var token = director.DefaultRequestHeaders.Authorization!.Parameter!;
        var parts = token.Split('.');

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", $"{parts[0]}.{parts[1]}.");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task A_token_stops_working_when_the_account_is_deactivated()
    {
        var user = await UserWithPasswordAsync(RoleNames.OperationsManager);
        var session = await (await LoginAsync(user.Email, Password)).Content.ReadFromJsonAsync<JsonElement>();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);

        await new Scenario(fixture.Factory).WithDbAsync(async db =>
        {
            var tracked = await db.Users.SingleAsync(u => u.UserId == user.UserId);
            tracked.IsActive = false;
            tracked.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    // ---- hostile text in free-text fields --------------------------------------------------------

    [Theory]
    [InlineData("<script>alert('x')</script>")]
    [InlineData("\"><img src=x onerror=alert(1)>")]
    [InlineData("'; DROP TABLE EVENT; --")]
    [InlineData("<svg/onload=alert(1)>")]
    [InlineData("javascript:alert(1)")]
    public async Task Hostile_text_in_an_event_name_is_stored_as_plain_text_and_sent_back_as_json(string payload)
    {
        var scenario = new Scenario(fixture.Factory);
        var (_, manager) = await scenario.SignedInAsync(RoleNames.EventManager);
        var reference = await scenario.ReferenceDataAsync();
        var body = Scenario.NewEventBody(reference);
        body["name"] = payload;

        var created = await manager.PostAsJsonAsync("/api/events", body);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var eventId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("eventId").GetGuid();

        var read = await manager.GetAsync($"/api/events/{eventId}");
        Assert.StartsWith("application/json", read.Content.Headers.ContentType?.MediaType ?? "");
        Assert.Equal("nosniff", read.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(payload, (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString());

        // The table is still there, so the text really was a parameter and never part of the SQL.
        Assert.True(await scenario.WithDbAsync(db => db.Events.AnyAsync(e => e.EventId == eventId)));
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
