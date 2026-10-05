using Carbonate.Domain.Common;

namespace Carbonate.Domain.Features.Commercial;

public enum EditOutcome
{
    /// <summary>Change the quote where it is.</summary>
    InPlace,

    /// <summary>The quote was approved or waiting for approval; it changes, so approval starts again.</summary>
    InPlaceAndReopen,

    /// <summary>The quote was issued, so its totals must never change: make a new version.</summary>
    NewVersion,

    Refused,
}

/// <summary>The commercial rules about when a quote may change status (FR-11, FR-14, FR-15).</summary>
public static class QuoteRules
{
    /// <summary>
    /// A quote above the approval threshold cannot be issued until the Director has approved it
    /// (FR-14). At or below the threshold no approval is needed.
    /// </summary>
    public static bool RequiresApproval(decimal totalIncVat, decimal threshold, Guid? approvedByUserId) =>
        totalIncVat > threshold && approvedByUserId is null;

    public static EditOutcome OnEdit(QuoteStatus status) => status switch
    {
        QuoteStatus.Draft => EditOutcome.InPlace,
        QuoteStatus.PendingApproval or QuoteStatus.Approved => EditOutcome.InPlaceAndReopen,
        QuoteStatus.Issued => EditOutcome.NewVersion,
        _ => EditOutcome.Refused,
    };

    public static bool CanSubmit(QuoteStatus status) => status == QuoteStatus.Draft;

    public static bool CanApprove(QuoteStatus status) => status == QuoteStatus.PendingApproval;

    public static bool CanIssue(QuoteStatus status) =>
        status is QuoteStatus.Draft or QuoteStatus.PendingApproval or QuoteStatus.Approved;

    public static bool CanAccept(QuoteStatus status) => status == QuoteStatus.Issued;
}
