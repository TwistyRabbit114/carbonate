using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Carbonate.Infrastructure.Seeding;

/// <summary>
/// Runs the platform seed once at startup. Turn it off with Seeding:Enabled=false. A failure is logged
/// and startup carries on: a serverless database that is waking up must not stop the host, and the
/// seed is safe to run again on the next start.
/// </summary>
internal sealed partial class PlatformSeedHostedService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<PlatformSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Seeding:Enabled", true))
        {
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PlatformSeeder>().SeedAsync(cancellationToken);

            // Templates hang off divisions, so they go after the platform seed, not alongside it (D, FR-25).
            await scope.ServiceProvider.GetRequiredService<TemplateSeeder>().SeedAsync(cancellationToken);

            // Demo data is opt-in and dev only: Seeding:Demo is set on carbonate-api-dev and never on
            // production. It runs last because it seeds events from the templates above (Appendix D).
            if (configuration.GetValue("Seeding:Demo", false))
            {
                await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSeedFailed(ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Error, Message = "Startup seed failed; the API will start without it and retry on the next start")]
    private partial void LogSeedFailed(Exception exception);
}
