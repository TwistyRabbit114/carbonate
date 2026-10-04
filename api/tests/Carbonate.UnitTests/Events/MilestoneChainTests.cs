using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Events;
using Shouldly;

namespace Carbonate.UnitTests.Events;

public class MilestoneChainTests
{
    private static readonly DateTime Starts = new(2026, 11, 14, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Ends = new(2026, 11, 14, 23, 0, 0, DateTimeKind.Utc);

    private static readonly MilestoneType[] Expected =
    [
        MilestoneType.SiteVisit, MilestoneType.LoadIn, MilestoneType.Rehearsal, MilestoneType.Doors,
        MilestoneType.Strike, MilestoneType.LoadOut, MilestoneType.Debrief, MilestoneType.Invoice,
        MilestoneType.Reconciliation,
    ];

    [Fact]
    public void The_chain_has_the_nine_milestones_in_the_planned_order()
    {
        var (milestones, _) = MilestoneChain.Build(Guid.NewGuid(), Starts, Ends);

        milestones.Select(m => m.MilestoneType).ShouldBe(Expected);
    }

    [Fact]
    public void Doors_starts_when_the_event_starts_and_Strike_starts_when_it_ends()
    {
        var (milestones, _) = MilestoneChain.Build(Guid.NewGuid(), Starts, Ends);

        milestones.Single(m => m.MilestoneType == MilestoneType.Doors).ScheduledStart.ShouldBe(Starts);
        milestones.Single(m => m.MilestoneType == MilestoneType.Strike).ScheduledStart.ShouldBe(Ends);
    }

    [Fact]
    public void Each_milestone_depends_on_the_one_before_with_no_lag()
    {
        var (milestones, dependencies) = MilestoneChain.Build(Guid.NewGuid(), Starts, Ends);

        dependencies.Count.ShouldBe(8);
        for (var i = 0; i < dependencies.Count; i++)
        {
            dependencies[i].PredecessorMilestoneId.ShouldBe(milestones[i].MilestoneId);
            dependencies[i].SuccessorMilestoneId.ShouldBe(milestones[i + 1].MilestoneId);
            dependencies[i].DependencyType.ShouldBe(DependencyType.FS);
            dependencies[i].LagHours.ShouldBe(0);
        }
    }

    [Fact]
    public void The_chain_satisfies_its_own_dependencies_so_nothing_needs_pushing()
    {
        var (milestones, dependencies) = MilestoneChain.Build(Guid.NewGuid(), Starts, Ends);
        var byId = milestones.ToDictionary(m => m.MilestoneId);

        foreach (var dependency in dependencies)
        {
            byId[dependency.SuccessorMilestoneId].ScheduledStart
                .ShouldBeGreaterThanOrEqualTo(byId[dependency.PredecessorMilestoneId].ScheduledEnd);
        }
    }

    [Fact]
    public void Every_milestone_has_a_valid_window_and_belongs_to_the_event()
    {
        var eventId = Guid.NewGuid();

        var (milestones, _) = MilestoneChain.Build(eventId, Starts, Ends);

        milestones.ShouldAllBe(m => m.EventId == eventId && m.ScheduledEnd >= m.ScheduledStart);
        milestones.ShouldAllBe(m => m.Status == MilestoneStatus.Planned);
    }

    [Fact]
    public void The_generated_chain_can_be_rescheduled_end_to_end()
    {
        var (milestones, dependencies) = MilestoneChain.Build(Guid.NewGuid(), Starts, Ends);
        var items = milestones.Select(m => new ScheduleItem(m.MilestoneId, m.ScheduledStart, m.ScheduledEnd)).ToList();
        var links = dependencies.Select(d => new ScheduleLink(d.PredecessorMilestoneId, d.SuccessorMilestoneId, TimeSpan.Zero)).ToList();
        var doors = milestones.Single(m => m.MilestoneType == MilestoneType.Doors);

        var result = ScheduleCalculator.Recalculate(items, links, doors.MilestoneId,
            doors.ScheduledStart.AddHours(2), doors.ScheduledEnd.AddHours(2));

        result.Succeeded.ShouldBeTrue();
        // Doors and everything after it move; the milestones before it do not.
        result.Moved.Count.ShouldBe(6);
    }
}
