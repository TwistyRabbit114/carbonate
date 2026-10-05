using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Platform.Users;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carbonate.IntegrationTests.Users;

[Collection(DatabaseCollection.Name)]
public class CrewExpiryServiceTests(DatabaseApiFixture fixture)
{
    private readonly Scenario _scenario = new(fixture.Factory);

    [Fact]
    public async Task An_account_whose_debrief_ended_eight_days_ago_is_deactivated_audited_and_signed_out()
    {
        var worker = await _scenario.UserAsync(RoleNames.CasualCrew);
        await WorkedAsync(worker, debriefEnded: Days(-8));
        await GiveRefreshTokenAsync(worker.UserId);

        var count = await RunAsync();

        Assert.True(count >= 1);
        var after = await UserAsync(worker.UserId);
        Assert.False(after.IsActive);
        Assert.NotEqual(worker.SecurityStamp, after.SecurityStamp);
        Assert.All(await _scenario.WithDbAsync(db => db.RefreshTokens.Where(t => t.UserId == worker.UserId).ToListAsync()),
            t => Assert.NotNull(t.RevokedAt));

        var entry = await _scenario.WithDbAsync(db => db.AuditEntries.SingleAsync(a => a.Action == "user.expired" && a.EntityId == worker.UserId.ToString()));
        Assert.Null(entry.UserId);
        Assert.Contains("expired", entry.AfterJson);
    }

    [Fact]
    public async Task An_account_stays_active_while_the_debrief_is_less_than_seven_days_ago()
    {
        var worker = await _scenario.UserAsync(RoleNames.CasualCrew);
        await WorkedAsync(worker, debriefEnded: Days(-5));

        await RunAsync();

        Assert.True((await UserAsync(worker.UserId)).IsActive);
    }

    [Fact]
    public async Task A_future_assignment_keeps_the_account_even_when_an_earlier_event_is_long_over()
    {
        var worker = await _scenario.UserAsync(RoleNames.CasualCrew);
        await WorkedAsync(worker, debriefEnded: Days(-30));
        await WorkedAsync(worker, debriefEnded: Days(12), shiftEnded: Days(10));

        await RunAsync();

        Assert.True((await UserAsync(worker.UserId)).IsActive);
    }

    [Fact]
    public async Task Only_accounts_holding_nothing_but_the_casual_crew_role_expire()
    {
        var lead = await _scenario.UserAsync(RoleNames.CasualCrew, RoleNames.CrewLead);
        var manager = await _scenario.UserAsync(RoleNames.EventManager);
        await WorkedAsync(lead, debriefEnded: Days(-30));
        await WorkedAsync(manager, debriefEnded: Days(-30));

        await RunAsync();

        Assert.True((await UserAsync(lead.UserId)).IsActive);
        Assert.True((await UserAsync(manager.UserId)).IsActive);
    }

    [Fact]
    public async Task The_actual_end_of_the_debrief_is_used_when_it_is_known()
    {
        var worker = await _scenario.UserAsync(RoleNames.CasualCrew);
        // Scheduled three days ago, but it actually finished nine days ago.
        await WorkedAsync(worker, debriefEnded: Days(-3), debriefActualEnd: Days(-9));

        await RunAsync();

        Assert.False((await UserAsync(worker.UserId)).IsActive);
    }

    [Fact]
    public async Task Accounts_with_no_assignments_or_an_event_with_no_debrief_are_left_alone()
    {
        var unassigned = await _scenario.UserAsync(RoleNames.CasualCrew);
        var noDebrief = await _scenario.UserAsync(RoleNames.CasualCrew);
        await WorkedAsync(noDebrief, debriefEnded: null);

        await RunAsync();

        Assert.True((await UserAsync(unassigned.UserId)).IsActive);
        Assert.True((await UserAsync(noDebrief.UserId)).IsActive);
    }

    [Fact]
    public async Task Running_it_again_does_not_deactivate_or_audit_the_same_account_twice()
    {
        var worker = await _scenario.UserAsync(RoleNames.CasualCrew);
        await WorkedAsync(worker, debriefEnded: Days(-20));

        await RunAsync();
        await RunAsync();

        var audits = await _scenario.WithDbAsync(db => db.AuditEntries.CountAsync(a => a.Action == "user.expired" && a.EntityId == worker.UserId.ToString()));
        Assert.Equal(1, audits);
    }

    [Fact]
    public async Task A_Director_can_bring_an_expired_account_back()
    {
        var (_, director) = await _scenario.SignedInAsync(RoleNames.Director);
        var worker = await _scenario.UserAsync(RoleNames.CasualCrew);
        await WorkedAsync(worker, debriefEnded: Days(-20));
        await RunAsync();
        Assert.False((await UserAsync(worker.UserId)).IsActive);

        var response = await director.PatchAsync($"/api/users/{worker.UserId}", System.Net.Http.Json.JsonContent.Create(new { isActive = true }));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.True((await UserAsync(worker.UserId)).IsActive);
    }

    private static DateTime Days(double fromNow) => DateTime.UtcNow.AddDays(fromNow);

    private async Task<int> RunAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CrewExpiryService>().RunAsync(CancellationToken.None);
    }

    private Task<AppUser> UserAsync(Guid userId) =>
        _scenario.WithDbAsync(db => db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId));

    private Task GiveRefreshTokenAsync(Guid userId) => _scenario.WithDbAsync(async db =>
    {
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            CreatedByIp = "127.0.0.1",
        });
        await db.SaveChangesAsync();
    });

    /// <summary>Assigns the worker to a new event. A null debrief end means the event has no Debrief milestone.</summary>
    private async Task WorkedAsync(AppUser worker, DateTime? debriefEnded, DateTime? shiftEnded = null, DateTime? debriefActualEnd = null)
    {
        var reference = await _scenario.ReferenceDataAsync();
        var creator = await _scenario.UserAsync(RoleNames.EventManager);
        var startsAt = (debriefEnded ?? Days(-30)).AddDays(-2);

        await _scenario.WithDbAsync(async db =>
        {
            var ev = new Event
            {
                EventCode = $"X-{Guid.NewGuid():N}"[..14],
                ClientId = reference.ClientId,
                VenueId = reference.VenueId,
                DivisionId = reference.DivisionId,
                CreatedByUserId = creator.UserId,
                Name = "Expiry test event",
                EventType = EventType.Corporate,
                EventDate = DateOnly.FromDateTime(startsAt),
                PackSizeEstimated = 50,
                PaymentMode = PaymentMode.PurchaseOrder,
                InfrastructureMode = InfrastructureMode.Owned,
                StaffRequired = 2,
                CreatedAt = DateTime.UtcNow,
                StartsAt = startsAt,
                EndsAt = startsAt.AddHours(6),
                Status = EventStatus.Finished,
            };
            db.Events.Add(ev);

            if (debriefEnded is { } end)
            {
                db.EventMilestones.Add(new EventMilestone
                {
                    EventId = ev.EventId,
                    MilestoneType = MilestoneType.Debrief,
                    ScheduledStart = end.AddHours(-1),
                    ScheduledEnd = end,
                    ActualEnd = debriefActualEnd,
                });
            }

            var shiftEnd = shiftEnded ?? startsAt.AddHours(8);
            db.CrewAssignments.Add(new CrewAssignment
            {
                EventId = ev.EventId,
                UserId = worker.UserId,
                CrewRole = "Bartender",
                ShiftStart = shiftEnd.AddHours(-8),
                ShiftEnd = shiftEnd,
            });
            await db.SaveChangesAsync();
        });
    }
}
