using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Carbonate.IntegrationTests.Support;

/// <summary>
/// Hosts the real API pipeline with test settings. No database is attached, so tests using it cover
/// the security behaviour that runs before any data is read.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public int LoginPermitPerMinute { get; init; } = 1000;

    /// <summary>The probe controllers exist only in tests, so the contract tests leave them out.</summary>
    public bool IncludeProbes { get; init; } = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:Issuer", "carbonate-tests");
        builder.UseSetting("Jwt:Audience", "carbonate-tests");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-at-least-32-chars");
        builder.UseSetting("Auth:MfaEncryptionKey", Convert.ToBase64String(new byte[32]));
        builder.UseSetting("Seeding:Enabled", "false");
        builder.UseSetting("ConnectionStrings:Sql", "Server=localhost;Database=unused;TrustServerCertificate=True");
        builder.UseSetting("RateLimiting:LoginPermitPerMinute", LoginPermitPerMinute.ToString());
        // A parseable endpoint so the blob client can be constructed. Nothing in the suite uploads, and
        // constructing BlobServiceClient makes no network call (D, FR-31).
        builder.UseSetting("Storage:BlobServiceUri", "https://carbonatetests.blob.core.windows.net");
        // The suite sends far more than 100 requests a minute from one address; the limiter has its own test.
        builder.UseSetting("RateLimiting:GlobalPermitPerMinute", "100000");

        // Never call the real breached-password service from tests. A password containing "breached" counts as breached.
        builder.ConfigureServices(services => services.AddSingleton<IPwnedPasswordChecker, OfflinePwnedChecker>());

        if (IncludeProbes)
        {
            builder.ConfigureServices(services => services
                .AddControllers()
                .AddApplicationPart(typeof(ApiFactory).Assembly));
        }
    }

    /// <summary>A client carrying a real access token for a user holding exactly these permissions.</summary>
    public HttpClient CreateClientWith(IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
    {
        var tokens = Services.GetRequiredService<ITokenService>();
        var token = tokens.CreateAccessToken(new AppUser { FullName = "Test", Email = "test@example.test" }, roles, permissions);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Value);
        return client;
    }

    public HttpClient CreateClientWithMfaToken()
    {
        var token = Services.GetRequiredService<ITokenService>().CreateMfaToken(Guid.NewGuid());
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}

internal sealed class OfflinePwnedChecker : IPwnedPasswordChecker
{
    public Task<bool> IsPwnedAsync(string password, CancellationToken ct) =>
        Task.FromResult(password.Contains("breached", StringComparison.OrdinalIgnoreCase));
}
