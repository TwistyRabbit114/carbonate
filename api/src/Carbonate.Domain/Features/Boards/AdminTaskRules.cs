namespace Carbonate.Domain.Features.Boards;

//----------------------------------------------------------\\
//                              TYPES
//----------------------------------------------------------\\

public enum AdminTaskAction
{
    HandIn,   //the assignee is done and wants it looked at: Assigned to In Progress / Needs Review
    Complete, //the manager signs it off: Needs Review to Complete
    Return,   //the manager sends it back with notes: Needs Review to Assigned
}

public enum AdminTaskVerdict
{
    Allowed,
    NotYours,          //the caller isn't the person this step belongs to, a 403
    WrongStage,        //right person, but the task isn't where this step starts, a 409
}

public sealed record AdminTaskDecision(AdminTaskVerdict Verdict, string? Reason)
{
    public static readonly AdminTaskDecision Allowed = new(AdminTaskVerdict.Allowed, null);
}

//----------------------------------------------------------\\
//                              RULES (FR-20)
//----------------------------------------------------------\\

//pure checks over the card, so they're tested without a database. who is checked before where, and
//"who" is always the creator's or assignee's id, never a role: holding a manager role doesn't let
//someone sign off a task they were given
public static class AdminTaskRules
{
    //the admin board's fixed columns, by position (plan section 8.5)
    public const int Assigned = 0;
    public const int NeedsReview = 1;
    public const int Complete = 2;

    public static AdminTaskDecision Check(
        AdminTaskAction action, int fromPosition, Guid callerId, Guid createdByUserId, IReadOnlyCollection<Guid> assigneeIds)
    {
        ArgumentNullException.ThrowIfNull(assigneeIds);
        var isAssignee = assigneeIds.Contains(callerId);

        return action switch
        {
            AdminTaskAction.HandIn when !isAssignee =>
                NotYours("Only someone this task is assigned to can hand it in."),
            AdminTaskAction.HandIn when fromPosition != Assigned =>
                WrongStage("Only a task in Assigned can be handed in for review."),

            AdminTaskAction.Complete or AdminTaskAction.Return when callerId != createdByUserId =>
                NotYours("Only the manager who handed out this task can complete or return it."),
            //TODO(plan): a manager who assigns a task to themselves can then never sign it off, because both
            //rules apply. should handing an admin task to yourself be refused when it's created? ask C
            AdminTaskAction.Complete or AdminTaskAction.Return when isAssignee =>
                NotYours("A task assigned to you has to be signed off by someone else."),
            AdminTaskAction.Complete or AdminTaskAction.Return when fromPosition != NeedsReview =>
                WrongStage("Only a task that's been handed in for review can be completed or returned."),

            _ => AdminTaskDecision.Allowed,
        };
    }

    private static AdminTaskDecision NotYours(string reason) => new(AdminTaskVerdict.NotYours, reason);

    private static AdminTaskDecision WrongStage(string reason) => new(AdminTaskVerdict.WrongStage, reason);
}
