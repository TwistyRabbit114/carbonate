using Azure.Identity;
using Azure.Storage.Blobs;
using Carbonate.Application.Features.Lifecycle;
using Carbonate.Application.Features.Stock;
using Carbonate.Application.Features.Venues;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Application.Platform.Files;
using Carbonate.Domain.Lifecycle;
using Carbonate.Infrastructure.Features.Lifecycle;
using Carbonate.Infrastructure.Features.Stock;
using Carbonate.Infrastructure.Features.Venues;
using Carbonate.Infrastructure.Persistence;
using Carbonate.Infrastructure.Platform.Audit;
using Carbonate.Infrastructure.Platform.Auth;
using Carbonate.Infrastructure.Platform.Files;
using Carbonate.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Carbonate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CemDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Sql")));

        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.Section));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<ISecretProtector, SecretProtector>();
        services.AddHttpClient<IPwnedPasswordChecker, PwnedPasswordChecker>(client =>
        {
            client.BaseAddress = new Uri("https://api.pwnedpasswords.com/");
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        // Event lifecycle (D, FR-02). The observers are resolved as a set, so adding one is a
        // registration here and nothing else - the state machine references none of them.
        services.AddScoped<IEventLifecycleService, EventLifecycleService>();
        services.AddScoped<IEventStateObserver, AuditObserver>();
        services.AddScoped<IEventStateObserver, CalendarSyncObserver>();
        services.AddScoped<IEventStateObserver, NotificationObserver>();
        services.AddHostedService<EventTransitionWorker>();

        // Stock and templates (D, FR-24-30)
        services.AddScoped<IEventTemplateSeeder, EventTemplateSeeder>();

        //venues and site visits (B, FR-32, FR-33)
        services.AddScoped<IVenueRepository, VenueRepository>();

        // Files (D, FR-31; FR-06 and FR-22 use it too)
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.Section));
        services.AddSingleton(provider =>
        {
            var uri = provider.GetRequiredService<IOptions<FileStorageOptions>>().Value.BlobServiceUri;
            // Managed identity in Azure; whatever the developer is signed in with locally.
            return new BlobServiceClient(new Uri(uri), new DefaultAzureCredential());
        });
        services.AddScoped<IFileStorage, BlobFileStorage>();

        services.AddScoped<PlatformSeeder>();
        services.AddScoped<TemplateSeeder>();
        services.AddHostedService<PlatformSeedHostedService>();

        return services;
    }
}
