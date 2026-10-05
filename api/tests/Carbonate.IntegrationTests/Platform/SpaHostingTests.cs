using System.Net;
using System.Text;
using Carbonate.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;

namespace Carbonate.IntegrationTests.Platform;

/// <summary>
/// The API serves the built app from wwwroot so the page and the API share one origin (decisions D-003).
/// These run against a small stand-in for the real build output.
/// </summary>
public class SpaHostingTests : IClassFixture<SpaHostingTests.SpaFactory>, IDisposable
{
    private const string IndexHtml = "<!doctype html><html><body><div id=\"root\">Carbonate app shell</div></body></html>";

    private readonly SpaFactory _factory;

    public SpaHostingTests(SpaFactory factory) => _factory = factory;

    public class SpaFactory : ApiFactory
    {
        public string WebRoot { get; } = Path.Combine(Path.GetTempPath(), $"carbonate-web-{Guid.NewGuid():N}");

        public SpaFactory()
        {
            Directory.CreateDirectory(Path.Combine(WebRoot, "assets"));
            File.WriteAllText(Path.Combine(WebRoot, "index.html"), IndexHtml);
            File.WriteAllText(Path.Combine(WebRoot, "assets", "app.abc123.js"), "console.log('app');");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseWebRoot(WebRoot);
            builder.UseSetting("Storage:BlobServiceUri", "https://carbonatestore.blob.core.windows.net/files");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(WebRoot))
            {
                Directory.Delete(WebRoot, recursive: true);
            }
        }
    }

    public void Dispose() => GC.SuppressFinalize(this);

    [Fact]
    public async Task The_app_is_served_at_the_root_without_signing_in()
    {
        var response = await _factory.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Carbonate app shell", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/events")]
    [InlineData("/events/7c9e6679-7425-40de-944b-e07fc1f90ae7")]
    [InlineData("/login")]
    [InlineData("/settings/users")]
    public async Task A_client_side_route_gets_the_app_so_a_page_refresh_works(string route)
    {
        var response = await _factory.CreateClient().GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Carbonate app shell", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Built_assets_are_cached_for_good_and_the_page_never_is()
    {
        var client = _factory.CreateClient();

        var asset = await client.GetAsync("/assets/app.abc123.js");
        var page = await client.GetAsync("/");
        var route = await client.GetAsync("/events/1");

        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", asset.Headers.CacheControl?.ToString());
        Assert.Equal("no-cache", page.Headers.CacheControl?.ToString());
        Assert.Equal("no-cache", route.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task The_page_and_its_files_carry_the_content_security_policy_from_the_plan()
    {
        var client = _factory.CreateClient();

        foreach (var path in new[] { "/", "/events/1", "/assets/app.abc123.js" })
        {
            var policy = (await client.GetAsync(path)).Headers.GetValues("Content-Security-Policy").Single();

            Assert.Contains("default-src 'self'", policy);
            Assert.Contains("script-src 'self'", policy);
            Assert.Contains("style-src 'self'", policy);
            Assert.Contains("object-src 'none'", policy);
            Assert.Contains("font-src 'self' data:", policy);
            Assert.Contains("frame-ancestors 'none'", policy);
            Assert.Contains("report-uri /api/csp-report", policy);
            Assert.DoesNotContain("unsafe-inline", policy);
            Assert.DoesNotContain("unsafe-eval", policy);
        }
    }

    [Fact]
    public async Task Images_may_come_from_the_blob_storage_host_and_nowhere_else()
    {
        var policy = (await _factory.CreateClient().GetAsync("/")).Headers.GetValues("Content-Security-Policy").Single();

        Assert.Contains("img-src 'self' data: https://carbonatestore.blob.core.windows.net;", policy);
        Assert.DoesNotContain("https://carbonatestore.blob.core.windows.net/files", policy);
    }

    [Theory]
    [InlineData("/api/not-a-route")]
    [InlineData("/api")]
    [InlineData("/openapi/v1.json.bak")]
    public async Task A_wrong_api_address_is_an_error_and_never_a_web_page(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.DoesNotContain("Carbonate app shell", await response.Content.ReadAsStringAsync());
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_app_fallback_does_not_open_up_the_api()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/events")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task API_responses_do_not_carry_the_page_policy_and_are_never_cached()
    {
        var response = await _factory.CreateClient().GetAsync("/api/me");

        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task A_browser_can_report_a_violation_without_signing_in()
    {
        var report = """{"csp-report":{"violated-directive":"script-src 'self'","blocked-uri":"https://evil.example/x.js?token=secret","document-uri":"https://app.example/events/1#top"}}""";

        var response = await _factory.CreateClient().PostAsync("/api/csp-report",
            new StringContent(report, Encoding.UTF8, "application/csp-report"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task The_newer_report_format_and_a_malformed_report_are_both_accepted_quietly()
    {
        var client = _factory.CreateClient();
        var modern = """[{"type":"csp-violation","body":{"effectiveDirective":"script-src","blockedURL":"inline"}}]""";

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsync("/api/csp-report", new StringContent(modern, Encoding.UTF8, "application/reports+json"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsync("/api/csp-report", new StringContent("{ not json", Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Fact]
    public async Task A_huge_violation_report_is_refused()
    {
        var huge = new StringContent(new string('x', 40_000), Encoding.UTF8, "application/csp-report");

        var response = await _factory.CreateClient().PostAsync("/api/csp-report", huge);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Reporting_only_accepts_posts()
    {
        var response = await _factory.CreateClient().GetAsync("/api/csp-report");

        // Not a 204: an unknown route is refused by deny-by-default before anything else.
        Assert.NotEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/assets/does-not-exist.js")]
    [InlineData("/favicon.ico")]
    [InlineData("/events/photo.png")]
    public async Task A_missing_file_is_not_answered_with_the_app(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Carbonate app shell", await response.Content.ReadAsStringAsync());
    }
}
