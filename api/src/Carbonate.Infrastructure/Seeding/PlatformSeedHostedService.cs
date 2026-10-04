using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Carbonate.Infrastructure.Seeding;

/// <summary>Runs the platform seed once at startup. Turn it off with Seeding:Enabled=false.</summary>
internal sealed class PlatformSeedHostedService(IServiceScopeFactory scopes, IConfiguration configuration)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Seeding:Enabled", true))
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformSeeder>().SeedAsync(cancellationToken);

        // Templates hang off divisions, so they go after the platform seed, not alongside it (D, FR-25).
        await scope.ServiceProvider.GetRequiredService<TemplateSeeder>().SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
