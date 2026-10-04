using Carbonate.Application.Features.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Carbonate.Infrastructure.Features.Lifecycle;

/// <summary>
/// Moves events to <c>InProgress</c> and <c>Finished</c> as their scheduled times pass (FR-02).
/// </summary>
/// <remarks>
/// <para>
/// It <b>sleeps until the next event is due</b> rather than polling. This is a cost decision, not a
/// style one: Azure SQL serverless auto-pauses after an hour of inactivity, and that is the entire
/// cost case in docs/hosting.md. A worker that queried every minute would keep the database awake
/// permanently and roughly triple the bill.
/// </para>
/// <para>
/// The sleep is capped at 30 minutes so a newly created event is picked up without a restart, and
/// floored at a few seconds so a bug cannot turn this into a spin loop.
/// </para>
/// <para>Needs <b>Always On</b> on the App Service, which is why the plan is B1 and not F1.</para>
/// </remarks>
internal sealed class EventTransitionWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<EventTransitionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan MaxSleep = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MinSleep = TimeSpan.FromSeconds(5);

    /// <summary>How long to wait after a failure, so a database that is down is not hammered.</summary>
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Event transition worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;

            try
            {
                delay = await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let the loop die: a transient database failure must not leave events stuck
                // until someone notices and restarts the app.
                logger.LogError(ex, "Transition cycle failed. Retrying in {Backoff}.", ErrorBackoff);
                delay = ErrorBackoff;
            }

            try
            {
                await Task.Delay(delay, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Event transition worker stopped.");
    }

    /// <returns>How long to sleep before the next cycle.</returns>
    private async Task<TimeSpan> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IEventLifecycleService>();

        var moved = await lifecycle.ApplyDueTransitionsAsync(ct);
        if (moved > 0)
        {
            logger.LogInformation("Moved {Count} event(s) to their due state.", moved);
        }

        var nextDue = await lifecycle.NextDueAtAsync(ct);
        return SleepUntil(nextDue, clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Time until the next due event, clamped. Internal so the clamping can be tested without
    /// standing up a host.
    /// </summary>
    internal static TimeSpan SleepUntil(DateTime? nextDueAt, DateTime now)
    {
        if (nextDueAt is null)
        {
            // Nothing scheduled at all. Wake on the cap in case an event is created meanwhile.
            return MaxSleep;
        }

        var wait = nextDueAt.Value - now;

        if (wait <= TimeSpan.Zero)
        {
            // Already due. Something went wrong — this cycle should have moved it — so wait the floor
            // rather than spinning.
            return MinSleep;
        }

        return wait > MaxSleep ? MaxSleep : wait;
    }
}
