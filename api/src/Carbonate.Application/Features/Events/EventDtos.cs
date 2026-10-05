using System.Text.Json;
using System.Text.Json.Serialization;
using Carbonate.Application.Common;
using Carbonate.Application.Masking;
using Carbonate.Domain.Common;

namespace Carbonate.Application.Features.Events;

/// <summary>One row of GET /api/events. It carries no money, so any role that can see the event can see it.</summary>
public class EventListItem
{
    public Guid EventId { get; set; }
    public string EventCode { get; set; } = "";
    public string Name { get; set; } = "";
    public EventStatus Status { get; set; }
    public EventType EventType { get; set; }

    /// <summary>CE or CLM.</summary>
    public string DivisionCode { get; set; } = "";

    public DateOnly EventDate { get; set; }

    /// <summary>The live window. It drives the automatic stage moves.</summary>
    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    /// <summary>Null for an enquiry that has no venue yet.</summary>
    public string? VenueName { get; set; }

    public int PackSizeEstimated { get; set; }
    public int? PackSizeActual { get; set; }
    public bool IsConfidential { get; set; }

    /// <summary>Base64. Send it back on every update and transition.</summary>
    public string RowVersion { get; set; } = "";
}

public class EventDetail : EventListItem
{
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public Guid? VenueId { get; set; }
    public Guid DivisionId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public int? HeadcountExpected { get; set; }
    public int? HeadcountConfirmed { get; set; }
    public PaymentMode PaymentMode { get; set; }
    public InfrastructureMode InfrastructureMode { get; set; }
    public int StaffRequired { get; set; }

    /// <summary>The intra-day service schedule, validated against its JSON Schema (FR-05).</summary>
    public JsonElement? ServiceSchedule { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? BudgetAmount { get; set; }

    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class EventListQuery : PageQuery
{
    public EventStatus? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? ClientId { get; set; }

    /// <summary>Matches the event code or name.</summary>
    public string? Q { get; set; }

    /// <summary>True for the events board: leaves out Enquired events (FR-03).</summary>
    public bool Board { get; set; }
}

public class SaveEventRequest
{
    public string EventCode { get; set; } = "";
    public Guid ClientId { get; set; }
    public Guid? VenueId { get; set; }
    public Guid DivisionId { get; set; }
    public string Name { get; set; } = "";
    public EventType EventType { get; set; }
    public DateOnly EventDate { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int PackSizeEstimated { get; set; }
    public int? HeadcountExpected { get; set; }
    public PaymentMode PaymentMode { get; set; }
    public InfrastructureMode InfrastructureMode { get; set; }
    public int StaffRequired { get; set; }
    public bool IsConfidential { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? BudgetAmount { get; set; }
}

public class UpdateEventRequest : SaveEventRequest
{
    public string RowVersion { get; set; } = "";
}

public class MilestoneDto
{
    public Guid MilestoneId { get; set; }
    public Guid EventId { get; set; }
    public MilestoneType MilestoneType { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public MilestoneStatus Status { get; set; }

    /// <summary>Milestones that must finish before this one starts.</summary>
    public IReadOnlyList<Guid> PredecessorIds { get; set; } = [];
}

public class RescheduleRequest
{
    public DateTime NewStart { get; set; }
    public DateTime NewEnd { get; set; }
    public string RowVersion { get; set; } = "";
}

/// <summary>The milestones that moved, after the dependency cascade (FR-04).</summary>
public class ScheduleResultDto
{
    public IReadOnlyList<MilestoneDto> Milestones { get; set; } = [];
    public string RowVersion { get; set; } = "";
}

public class CrewAssignmentDto
{
    public Guid AssignmentId { get; set; }
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = "";
    public string CrewRole { get; set; } = "";
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public bool Confirmed { get; set; }

    [FinancialField(FinancialTier.Staff)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? HourlyRate { get; set; }
}

public class AssignCrewRequest
{
    public Guid UserId { get; set; }
    public string CrewRole { get; set; } = "";
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }

    [FinancialField(FinancialTier.Staff)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? HourlyRate { get; set; }
}

/// <summary>One earlier event for the same client, for comparing costs (FR-09).</summary>
public class CostHistoryItem
{
    public Guid EventId { get; set; }
    public string EventCode { get; set; } = "";
    public string Name { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public int PackSize { get; set; }

    [FinancialField(FinancialTier.Price)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? TotalIncVat { get; set; }

    [FinancialField(FinancialTier.Cost)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? InternalCostTotal { get; set; }

    [FinancialField(FinancialTier.Margin)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? MarginPercent { get; set; }
}

public class ServiceScheduleRequest
{
    public JsonElement Schedule { get; set; }
    public string RowVersion { get; set; } = "";
}

public class PackSizeRequest
{
    public int? PackSizeEstimated { get; set; }
    public int? PackSizeActual { get; set; }
    public string RowVersion { get; set; } = "";
}

public class EventDocumentDto
{
    public Guid DocumentId { get; set; }
    public Guid EventId { get; set; }
    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public SensitivityLevel SensitivityLevel { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }

    /// <summary>A short-lived link. Present only when the caller may open the document.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DownloadUrl { get; set; }
}

public class TransitionRequest
{
    public EventStatus To { get; set; }
    public string RowVersion { get; set; } = "";
}

public class AllowedTransitionsResponse
{
    public Guid EventId { get; set; }
    public EventStatus Current { get; set; }
    public IReadOnlyList<EventStatus> Allowed { get; set; } = [];
}

/// <summary>A client the event form can pick (FR-01).</summary>
public class ClientOption
{
    public Guid ClientId { get; set; }
    public string Name { get; set; } = "";
}

public class ClientListQuery : PageQuery
{
    /// <summary>Matches the start of the client name.</summary>
    public string? Q { get; set; }
}

public class DivisionOption
{
    public Guid DivisionId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>An active person who can be put on an event crew (FR-07). No contact details.</summary>
public class CrewCandidate
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = "";
    public IReadOnlyList<string> Roles { get; set; } = [];
}
