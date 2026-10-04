using Carbonate.Domain.Common;

namespace Carbonate.Domain.Features.Events;

/// <summary>
/// The default milestone chain every new event starts with (FR-01):
/// SiteVisit, LoadIn, Rehearsal, Doors, Strike, LoadOut, Debrief, Invoice, Reconciliation, each
/// finish-to-start with no lag. Doors starts when the event starts and Strike starts when it ends.
/// </summary>
public static class MilestoneChain
{
    // TODO(plan): the plan fixes Doors and Strike but not the other durations or gaps. These are
    // working defaults for the client to confirm; a manager changes any of them by rescheduling.
    private static readonly TimeSpan RecceLead = TimeSpan.FromDays(7);
    private static readonly TimeSpan RecceLength = TimeSpan.FromHours(2);
    private static readonly TimeSpan LoadInLead = TimeSpan.FromHours(5);
    private static readonly TimeSpan RehearsalLength = TimeSpan.FromHours(1);
    private static readonly TimeSpan StrikeLength = TimeSpan.FromHours(2);
    private static readonly TimeSpan LoadOutLength = TimeSpan.FromHours(2);
    private static readonly TimeSpan DebriefGap = TimeSpan.FromHours(24);
    private static readonly TimeSpan DebriefLength = TimeSpan.FromHours(1);
    private static readonly TimeSpan InvoiceGap = TimeSpan.FromHours(24);
    private static readonly TimeSpan InvoiceLength = TimeSpan.FromHours(1);
    private static readonly TimeSpan ReconciliationGap = TimeSpan.FromHours(72);
    private static readonly TimeSpan ReconciliationLength = TimeSpan.FromHours(1);

    public static (List<EventMilestone> Milestones, List<MilestoneDependency> Dependencies) Build(
        Guid eventId,
        DateTime startsAt,
        DateTime endsAt)
    {
        var rehearsalStart = startsAt - RehearsalLength;
        var loadInStart = startsAt - LoadInLead;
        var recceStart = loadInStart - RecceLead;
        var strikeEnd = endsAt + StrikeLength;
        var loadOutEnd = strikeEnd + LoadOutLength;
        var debriefStart = loadOutEnd + DebriefGap;
        var invoiceStart = debriefStart + DebriefLength + InvoiceGap;
        var reconciliationStart = invoiceStart + InvoiceLength + ReconciliationGap;

        var milestones = new List<EventMilestone>
        {
            New(eventId, MilestoneType.SiteVisit, recceStart, recceStart + RecceLength),
            New(eventId, MilestoneType.LoadIn, loadInStart, rehearsalStart),
            New(eventId, MilestoneType.Rehearsal, rehearsalStart, startsAt),
            New(eventId, MilestoneType.Doors, startsAt, endsAt),
            New(eventId, MilestoneType.Strike, endsAt, strikeEnd),
            New(eventId, MilestoneType.LoadOut, strikeEnd, loadOutEnd),
            New(eventId, MilestoneType.Debrief, debriefStart, debriefStart + DebriefLength),
            New(eventId, MilestoneType.Invoice, invoiceStart, invoiceStart + InvoiceLength),
            New(eventId, MilestoneType.Reconciliation, reconciliationStart, reconciliationStart + ReconciliationLength),
        };

        var dependencies = milestones.Zip(milestones.Skip(1), (before, after) => new MilestoneDependency
        {
            PredecessorMilestoneId = before.MilestoneId,
            SuccessorMilestoneId = after.MilestoneId,
            DependencyType = DependencyType.FS,
            LagHours = 0,
        }).ToList();

        return (milestones, dependencies);
    }

    private static EventMilestone New(Guid eventId, MilestoneType type, DateTime start, DateTime end) => new()
    {
        EventId = eventId,
        MilestoneType = type,
        ScheduledStart = start,
        ScheduledEnd = end,
        Status = MilestoneStatus.Planned,
    };
}
