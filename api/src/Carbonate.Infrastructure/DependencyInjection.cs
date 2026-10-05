using Azure.Identity;
using Azure.Storage.Blobs;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Boards;
using Carbonate.Application.Features.Calendar;
using Carbonate.Application.Features.Commercial;
using Carbonate.Application.Features.Events;
using Carbonate.Application.Features.Lifecycle;
using Carbonate.Application.Features.Stock;
using Carbonate.Application.Features.Venues;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Application.Platform.Files;
using Carbonate.Domain.Lifecycle;
using Carbonate.Infrastructure.Common;
using Carbonate.Infrastructure.Features.Boards;
using Carbonate.Infrastructure.Features.Calendar;
using Carbonate.Infrastructure.Features.Commercial;
using Carbonate.Infrastructure.Features.Events;
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
        services.Configure<FinanceOptions>(configuration.GetSection(FinanceOptions.Section));
        services.Configure<QuoteOptions>(configuration.GetSection(QuoteOptions.Section));
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<ICommercialRepository, CommercialRepository>();
        services.AddScoped<ITransactionRunner, TransactionRunner>();
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

        // Calendar sync (D, FR-40-42). Outbound only: the worker drains CALENDAR_OUTBOX so no user
        // request ever waits on Google (NFR-12).
        services.AddScoped<ICalendarClient, UnconfiguredCalendarClient>();
        services.AddSingleton<ICalendarStateTokens, CalendarStateTokens>();
        services.AddHostedService<CalendarSyncWorker>();

        // Stock and templates (D, FR-24-30)
        services.AddScoped<IEventTemplateSeeder, EventTemplateSeeder>();

        //event visibility and description cleaning, shared by every module that needs them (plan sections 7.2, 7.5)
        services.AddScoped<IEventAccess, EventAccess>();
        services.AddSingleton<IHtmlSanitiser, HtmlSanitiser>();

        //boards and cards (B, FR-19 to FR-21)
        services.AddScoped<IBoardRepository, BoardRepository>();

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
