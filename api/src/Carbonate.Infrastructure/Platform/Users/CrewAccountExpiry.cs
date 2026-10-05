using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Application.Platform.Users;
using Carbonate.Domain.Common;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Carbonate.Infrastructure.Platform.Users;

/// <summary>
/// Deactivates casual crew accounts whose work is over (FR-36, NFR-21). The decision is
/// <see cref="CrewExpiry"/>; this finds the candidates, applies it, and records each deactivation.
/// </summary>
internal sealed class CrewExpiryService(
    CemDbContext db,
    IRefreshTokenRepository refreshTokens,
    IAuditService audit,
    IOptions<CrewOptions> options,
    TimeProvider clock)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var grace = TimeSpan.FromDays(options.Value.DeactivateAfterDays);

        // Active accounts that hold the casual crew role and nothing else.
        var candidates = await db.Users
            .Where(u => u.IsActive
                        && u.UserRoles.Any()
                        && u.UserRoles.All(ur => ur.Role.Name == RoleNames.CasualCrew))
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return 0;
        }

        // One query for everything they are assigned to, with each event's debrief end, not one per person.
        var ids = candidates.Select(u => u.UserId).ToList();
        var facts = await (
                from c in db.CrewAssignments.AsNoTracking()
                where ids.Contains(c.UserId)
                join m in db.EventMilestones.Where(m => m.MilestoneType == MilestoneType.Debrief)
                    on c.EventId equals m.EventId into debriefs
                from debrief in debriefs.DefaultIfEmpty()
                select new
                {
                    c.UserId,
                    c.ShiftEnd,
                    DebriefEnd = debrief == null ? (DateTime?)null : debrief.ActualEnd ?? debrief.ScheduledEnd,
                })
            .ToListAsync(ct);

        var byUser = facts.ToLookup(f => f.UserId);
        var deactivated = 0;

        foreach (var account in candidates)
        {
            var assignments = byUser[account.UserId].Select(f => new CrewAssignmentFact(f.ShiftEnd, f.DebriefEnd)).ToList();
            if (!CrewExpiry.ShouldDeactivate(true, assignments, now, grace))
            {
                continue;
            }

            account.IsActive = false;
            account.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync(ct);
            await refreshTokens.RevokeAllForUserAsync(account.UserId, now, ct);

            // No signed-in user did this, so the audit entry has no actor: it is the system.
            await audit.RecordAsync("user.expired", nameof(AppUser), account.UserId.ToString(),
                new { IsActive = true }, new { IsActive = false, Reason = "Casual crew account expired after debrief" },
                null, ct);
            deactivated++;
        }

        return deactivated;
    }
}

/// <summary>
/// Runs <see cref="CrewExpiryService"/> once a day (FR-36). Like the transition worker it sleeps until the
/// next run rather than polling, so it never keeps a serverless database awake.
/// </summary>
internal sealed partial class CrewAccountExpiryWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<CrewAccountExpiryWorker> logger) : BackgroundService
{
    /// <summary>02:30 in Cape Town, when nobody is working an event.</summary>
    private static readonly TimeSpan RunAtUtc = new(0, 30, 0);

    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextRunDelay(clock.GetUtcNow().UtcDateTime);

            try
            {
                await Task.Delay(delay, clock, stoppingToken);
                await using var scope = scopes.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<CrewExpiryService>().RunAsync(stoppingToken);
                if (count > 0)
                {
                    LogDeactivated(count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed night is retried, never fatal: the loop must keep running.
                LogFailed(ex);
                try
                {
                    await Task.Delay(ErrorBackoff, clock, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Time until the next 02:30 Cape Town, always in the future. Internal so it can be tested.</summary>
    internal static TimeSpan NextRunDelay(DateTime nowUtc)
    {
        var next = nowUtc.Date + RunAtUtc;
        if (next <= nowUtc)
        {
            next = next.AddDays(1);
        }

        return next - nowUtc;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deactivated {Count} casual crew account(s) whose events are over.")]
    private partial void LogDeactivated(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Crew account expiry failed; it will run again.")]
    private partial void LogFailed(Exception exception);
}
