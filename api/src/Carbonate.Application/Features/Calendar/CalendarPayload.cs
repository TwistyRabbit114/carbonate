using Carbonate.Domain.Common;

namespace Carbonate.Application.Features.Calendar;

/// <summary>
/// What Carbonate pushes to Google (FR-42). Deliberately thin.
/// </summary>
/// <remarks>
/// Google Calendar is outside Carbonate's security boundary: entries are visible to anyone the
/// calendar is shared with, and Google's own retention applies. So the payload carries <b>no client
/// names, no contact details and no money values</b> — only what someone needs to know that the
/// Carbon team is committed at that time.
/// </remarks>
/// <param name="Title">Shown in the calendar grid.</param>
/// <param name="Description">Longer text. May be empty.</param>
/// <param name="Location">The venue name only — never an address with contact details.</param>
public readonly record struct CalendarEventPayload(
    string Title,
    string Description,
    string? Location,
    DateTime StartsAt,
    DateTime EndsAt);

/// <summary>Builds the payload. Pure, so the redaction rules can be tested exhaustively.</summary>
public static class CalendarPayloadBuilder
{
    /// <summary>What a confidential event's title says instead of its name (FR-42).</summary>
    public const string ConfidentialPrefix = "Confidential event";

    /// <param name="isConfidential">
    /// True when the event is marked confidential <b>or</b> the client requires confidentiality. The
    /// caller resolves both into this one flag.
    /// </param>
    public static CalendarEventPayload ForEvent(
        string eventCode,
        string eventName,
        EventType eventType,
        DateTime startsAt,
        DateTime endsAt,
        string? venueName,
        bool isConfidential)
    {
        if (isConfidential)
        {
            // The code, the time and the venue are all the team needs to avoid double-booking. The
            // name of a confidential event is exactly what must not leak into a shared calendar.
            return new CalendarEventPayload(
                $"{ConfidentialPrefix} — {eventCode}",
                "Details are in Carbonate.",
                venueName,
                startsAt,
                endsAt);
        }

        return new CalendarEventPayload(
            $"{eventCode} — {eventName}",
            // Event type, not pax, budget or client: a headcount hints at the deal size.
            $"{eventType} event. Details are in Carbonate.",
            venueName,
            startsAt,
            endsAt);
    }

    /// <summary>A milestone on the run sheet: load-in, doors, strike and the rest.</summary>
    public static CalendarEventPayload ForMilestone(
        string eventCode,
        string eventName,
        MilestoneType milestoneType,
        DateTime startsAt,
        DateTime endsAt,
        string? venueName,
        bool isConfidential)
    {
        var subject = isConfidential ? ConfidentialPrefix : eventName;

        return new CalendarEventPayload(
            $"{eventCode} — {Label(milestoneType)}",
            $"{Label(milestoneType)} for {subject}. Details are in Carbonate.",
            venueName,
            startsAt,
            // Google rejects an entry that ends before it starts, and a milestone with no duration
            // would be invisible in a day view.
            endsAt > startsAt ? endsAt : startsAt.AddMinutes(30));
    }

    /// <summary>The client's words, not the enum's (Appendix C).</summary>
    private static string Label(MilestoneType type) => type switch
    {
        MilestoneType.SiteVisit => "Recce",
        MilestoneType.LoadIn => "Load-in",
        MilestoneType.Doors => "Doors",
        MilestoneType.Strike => "Strike",
        MilestoneType.LoadOut => "Load-out",
        MilestoneType.Debrief => "Debrief",
        MilestoneType.Rehearsal => "Rehearsal",
        MilestoneType.Invoice => "Invoice due",
        MilestoneType.Reconciliation => "Reconciliation",
        _ => type.ToString(),
    };
}
