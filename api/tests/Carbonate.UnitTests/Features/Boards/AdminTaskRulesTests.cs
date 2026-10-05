using Carbonate.Domain.Features.Boards;
using Shouldly;

namespace Carbonate.UnitTests.Features.Boards;

//FR-20: the assignee hands a task in, and only the manager who handed it out completes or returns it
public class AdminTaskRulesTests
{
    private static readonly Guid Manager = Guid.NewGuid();
    private static readonly Guid Assignee = Guid.NewGuid();
    private static readonly Guid OtherManager = Guid.NewGuid();

    private static AdminTaskVerdict Check(AdminTaskAction action, int from, Guid caller, params Guid[] assignees) =>
        AdminTaskRules.Check(action, from, caller, Manager, assignees.Length == 0 ? [Assignee] : assignees).Verdict;

    //----------------------------------------------------------\\
    //                              HANDING IN
    //----------------------------------------------------------\\

    [Fact]
    public void The_assignee_hands_a_task_in_from_assigned() =>
        Check(AdminTaskAction.HandIn, AdminTaskRules.Assigned, Assignee).ShouldBe(AdminTaskVerdict.Allowed);

    [Theory]
    [InlineData(AdminTaskRules.NeedsReview)]
    [InlineData(AdminTaskRules.Complete)]
    public void A_task_already_handed_in_or_done_cannot_be_handed_in(int from) =>
        Check(AdminTaskAction.HandIn, from, Assignee).ShouldBe(AdminTaskVerdict.WrongStage);

    [Fact]
    public void Nobody_else_can_hand_it_in_not_even_the_manager() =>
        Check(AdminTaskAction.HandIn, AdminTaskRules.Assigned, Manager).ShouldBe(AdminTaskVerdict.NotYours);

    //----------------------------------------------------------\\
    //                              COMPLETING AND RETURNING
    //----------------------------------------------------------\\

    [Theory]
    [InlineData(AdminTaskAction.Complete)]
    [InlineData(AdminTaskAction.Return)]
    public void The_creating_manager_signs_off_or_returns_a_handed_in_task(AdminTaskAction action) =>
        Check(action, AdminTaskRules.NeedsReview, Manager).ShouldBe(AdminTaskVerdict.Allowed);

    [Theory]
    [InlineData(AdminTaskAction.Complete, AdminTaskRules.Assigned)]
    [InlineData(AdminTaskAction.Return, AdminTaskRules.Assigned)]
    [InlineData(AdminTaskAction.Complete, AdminTaskRules.Complete)]
    public void Only_a_handed_in_task_can_be_signed_off_or_returned(AdminTaskAction action, int from) =>
        Check(action, from, Manager).ShouldBe(AdminTaskVerdict.WrongStage);

    [Theory]
    [InlineData(AdminTaskAction.Complete)]
    [InlineData(AdminTaskAction.Return)]
    public void A_different_manager_cannot_sign_off_or_return_it(AdminTaskAction action) =>
        Check(action, AdminTaskRules.NeedsReview, OtherManager).ShouldBe(AdminTaskVerdict.NotYours);

    //the check is on ids, so an assignee who also holds a manager role still can't sign off their own task
    [Fact]
    public void The_assignee_can_never_complete_their_own_task() =>
        Check(AdminTaskAction.Complete, AdminTaskRules.NeedsReview, Assignee).ShouldBe(AdminTaskVerdict.NotYours);

    [Fact]
    public void A_manager_who_assigned_the_task_to_themselves_cannot_sign_it_off_either() =>
        Check(AdminTaskAction.Complete, AdminTaskRules.NeedsReview, Manager, Manager)
            .ShouldBe(AdminTaskVerdict.NotYours);

    [Fact]
    public void Who_is_checked_before_where()
    {
        //an outsider trying the wrong stage hears that it isn't theirs, not when it could be done
        Check(AdminTaskAction.Complete, AdminTaskRules.Assigned, OtherManager).ShouldBe(AdminTaskVerdict.NotYours);
    }

    [Fact]
    public void Every_refusal_says_why()
    {
        var decision = AdminTaskRules.Check(AdminTaskAction.Return, AdminTaskRules.NeedsReview, OtherManager, Manager, [Assignee]);

        decision.Reason.ShouldNotBeNullOrWhiteSpace();
    }
}
