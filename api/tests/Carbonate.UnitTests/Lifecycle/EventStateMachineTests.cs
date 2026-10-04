using Carbonate.Domain.Common;
using Carbonate.Domain.Lifecycle;
using Shouldly;

namespace Carbonate.UnitTests.Lifecycle;

/// <summary>FR-02. The transition table in plan section 8.3, and everything it rules out.</summary>
public class EventStateMachineTests
{
    private static readonly DateTime StartsAt = new(2026, 11, 14, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EndsAt = new(2026, 11, 15, 2, 0, 0, DateTimeKind.Utc);

    private static TransitionContext Context(
        TransitionTrigger trigger = TransitionTrigger.Manual,
        DateTime? now = null,
        bool hasConfirmation = false) =>
        new(trigger, now ?? StartsAt.AddDays(-7), StartsAt, EndsAt, hasConfirmation);

    // ---- The four transitions that exist -------------------------------------------------

    [Fact]
    public void Enquired_moves_to_confirmed_once_a_confirmation_exists() =>
        EventStateMachine.Check(
                EventStatus.Enquired,
                EventStatus.ConfirmedInPlanning,
                Context(TransitionTrigger.Confirmation, hasConfirmation: true))
            .Allowed.ShouldBeTrue();

    [Fact]
    public void Confirmed_moves_to_in_progress_automatically_once_it_has_started() =>
        EventStateMachine.Check(
                EventStatus.ConfirmedInPlanning,
                EventStatus.InProgress,
                Context(TransitionTrigger.Scheduled, now: StartsAt))
            .Allowed.ShouldBeTrue();

    [Fact]
    public void Confirmed_moves_to_in_progress_manually_before_it_has_started()
    {
        // An early start on the day: the client is ready, so the crew starts. The clock does not
        // get a say when a person is asking.
        EventStateMachine.Check(
                EventStatus.ConfirmedInPlanning,
                EventStatus.InProgress,
                Context(TransitionTrigger.Manual, now: StartsAt.AddHours(-3)))
            .Allowed.ShouldBeTrue();
    }

    [Fact]
    public void Confirmed_moves_to_cancelled() =>
        EventStateMachine.Check(EventStatus.ConfirmedInPlanning, EventStatus.Cancelled, Context())
            .Allowed.ShouldBeTrue();

    [Fact]
    public void In_progress_moves_to_finished_automatically_once_it_has_ended() =>
        EventStateMachine.Check(
                EventStatus.InProgress,
                EventStatus.Finished,
                Context(TransitionTrigger.Scheduled, now: EndsAt))
            .Allowed.ShouldBeTrue();

    // ---- Preconditions -------------------------------------------------------------------

    [Fact]
    public void Enquired_cannot_be_confirmed_without_a_confirmation()
    {
        var result = EventStateMachine.Check(
            EventStatus.Enquired, EventStatus.ConfirmedInPlanning, Context(hasConfirmation: false));

        result.Allowed.ShouldBeFalse();
        // 422, not 409: the move is legal, the paperwork is missing.
        result.Code.ShouldBe(TransitionCodes.ConfirmationRequired);
    }

    [Fact]
    public void The_worker_does_not_start_an_event_early()
    {
        var result = EventStateMachine.Check(
            EventStatus.ConfirmedInPlanning,
            EventStatus.InProgress,
            Context(TransitionTrigger.Scheduled, now: StartsAt.AddMinutes(-1)));

        result.Allowed.ShouldBeFalse();
        result.Code.ShouldBe(TransitionCodes.NotYetDue);
    }

    [Fact]
    public void The_worker_does_not_finish_an_event_early()
    {
        var result = EventStateMachine.Check(
            EventStatus.InProgress,
            EventStatus.Finished,
            Context(TransitionTrigger.Scheduled, now: EndsAt.AddMinutes(-1)));

        result.Allowed.ShouldBeFalse();
        result.Code.ShouldBe(TransitionCodes.NotYetDue);
    }

    [Fact]
    public void Nothing_automatic_can_cancel_an_event()
    {
        var result = EventStateMachine.Check(
            EventStatus.ConfirmedInPlanning, EventStatus.Cancelled, Context(TransitionTrigger.Scheduled));

        result.Allowed.ShouldBeFalse();
        result.Code.ShouldBe(TransitionCodes.ManualOnly);
    }

    // ---- Everything the plan rules out ---------------------------------------------------

    [Theory]
    // FR-02: Cancelled is reachable only from ConfirmedInPlanning.
    [InlineData(EventStatus.Enquired, EventStatus.Cancelled)]
    [InlineData(EventStatus.InProgress, EventStatus.Cancelled)]
    [InlineData(EventStatus.Finished, EventStatus.Cancelled)]
    // No skipping the middle.
    [InlineData(EventStatus.Enquired, EventStatus.InProgress)]
    [InlineData(EventStatus.Enquired, EventStatus.Finished)]
    [InlineData(EventStatus.ConfirmedInPlanning, EventStatus.Finished)]
    // No going backwards.
    [InlineData(EventStatus.InProgress, EventStatus.ConfirmedInPlanning)]
    [InlineData(EventStatus.Finished, EventStatus.InProgress)]
    [InlineData(EventStatus.ConfirmedInPlanning, EventStatus.Enquired)]
    // Terminal means terminal.
    [InlineData(EventStatus.Cancelled, EventStatus.ConfirmedInPlanning)]
    [InlineData(EventStatus.Cancelled, EventStatus.InProgress)]
    [InlineData(EventStatus.Finished, EventStatus.ConfirmedInPlanning)]
    public void Refuses_a_transition_that_is_not_on_the_map(EventStatus from, EventStatus to)
    {
        var result = EventStateMachine.Check(
            from, to, Context(TransitionTrigger.Manual, now: StartsAt, hasConfirmation: true));

        result.Allowed.ShouldBeFalse();
        // 409, not 422: no amount of paperwork makes this legal.
        result.Code.ShouldBe(TransitionCodes.InvalidTransition);
    }

    [Theory]
    [InlineData(EventStatus.Enquired)]
    [InlineData(EventStatus.ConfirmedInPlanning)]
    [InlineData(EventStatus.InProgress)]
    [InlineData(EventStatus.Finished)]
    [InlineData(EventStatus.Cancelled)]
    public void Refuses_a_move_to_the_state_it_is_already_in(EventStatus status)
    {
        // Otherwise a double-click would publish a state-changed event for a state that did not change,
        // and every observer would act on it.
        EventStateMachine.Check(status, status, Context(hasConfirmation: true)).Allowed.ShouldBeFalse();
    }

    [Theory]
    [InlineData(EventStatus.Finished)]
    [InlineData(EventStatus.Cancelled)]
    public void Terminal_states_have_nowhere_to_go(EventStatus status) =>
        EventStateMachine.For(status).IsTerminal.ShouldBeTrue();

    // ---- What the SPA asks for -----------------------------------------------------------

    [Fact]
    public void Allowed_transitions_for_an_unconfirmed_enquiry_is_empty() =>
        EventStateMachine.AllowedFrom(EventStatus.Enquired, Context(hasConfirmation: false)).ShouldBeEmpty();

    [Fact]
    public void Allowed_transitions_for_a_confirmed_event_are_both_exits() =>
        EventStateMachine.AllowedFrom(EventStatus.ConfirmedInPlanning, Context())
            .ShouldBe([EventStatus.InProgress, EventStatus.Cancelled], ignoreOrder: true);

    [Fact]
    public void Allowed_transitions_for_a_finished_event_is_empty() =>
        EventStateMachine.AllowedFrom(EventStatus.Finished, Context()).ShouldBeEmpty();

    // ---- What the worker asks for --------------------------------------------------------

    [Fact]
    public void Nothing_is_due_before_the_start_time() =>
        EventStateMachine.DueStatus(
            EventStatus.ConfirmedInPlanning, StartsAt.AddMinutes(-1), StartsAt, EndsAt).ShouldBeNull();

    [Fact]
    public void In_progress_is_due_at_the_start_time() =>
        EventStateMachine.DueStatus(EventStatus.ConfirmedInPlanning, StartsAt, StartsAt, EndsAt)
            .ShouldBe(EventStatus.InProgress);

    [Fact]
    public void A_missed_window_still_goes_through_in_progress_first()
    {
        // The app was down over the weekend. The event must not jump straight to Finished, or the
        // calendar and the notifications never see it start.
        EventStateMachine.DueStatus(EventStatus.ConfirmedInPlanning, EndsAt.AddDays(3), StartsAt, EndsAt)
            .ShouldBe(EventStatus.InProgress);
    }

    [Fact]
    public void Finished_is_due_at_the_end_time() =>
        EventStateMachine.DueStatus(EventStatus.InProgress, EndsAt, StartsAt, EndsAt)
            .ShouldBe(EventStatus.Finished);

    [Theory]
    [InlineData(EventStatus.Enquired)]
    [InlineData(EventStatus.Finished)]
    [InlineData(EventStatus.Cancelled)]
    public void Nothing_is_ever_due_for_these(EventStatus status) =>
        EventStateMachine.DueStatus(status, EndsAt.AddYears(1), StartsAt, EndsAt).ShouldBeNull();

    [Fact]
    public void Next_due_for_a_confirmed_event_is_its_start() =>
        EventStateMachine.NextDueAt(EventStatus.ConfirmedInPlanning, StartsAt, EndsAt).ShouldBe(StartsAt);

    [Fact]
    public void Next_due_for_a_running_event_is_its_end() =>
        EventStateMachine.NextDueAt(EventStatus.InProgress, StartsAt, EndsAt).ShouldBe(EndsAt);

    [Theory]
    [InlineData(EventStatus.Enquired)]
    [InlineData(EventStatus.Finished)]
    [InlineData(EventStatus.Cancelled)]
    public void These_never_need_the_worker_again(EventStatus status) =>
        EventStateMachine.NextDueAt(status, StartsAt, EndsAt).ShouldBeNull();
}
