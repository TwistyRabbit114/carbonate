using Carbonate.Domain.Common;

namespace Carbonate.Domain.Features.Events;

public class Client
{
    public Guid ClientId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? TradingName { get; set; }
    public string? VatNumber { get; set; }
    public int PaymentTermsDays { get; set; }
    public bool ConfidentialityRequired { get; set; }
    public bool IsActive { get; set; } = true;

    public List<ClientContact> Contacts { get; set; } = [];
}

public class ClientContact
{
    public Guid ContactId { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public string FullName { get; set; } = "";
    public string? Position { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

public class Division
{
    public Guid DivisionId { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class Event
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventCode { get; set; } = "";
    public Guid ClientId { get; set; }
    public Guid? VenueId { get; set; }
    public Guid DivisionId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Name { get; set; } = "";
    public EventType EventType { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Enquired;
    public DateOnly EventDate { get; set; }
    public int? HeadcountExpected { get; set; }
    public int? HeadcountConfirmed { get; set; }
    public int PackSizeEstimated { get; set; }
    public int? PackSizeActual { get; set; }
    public decimal? BudgetAmount { get; set; }
    public PaymentMode PaymentMode { get; set; }
    public InfrastructureMode InfrastructureMode { get; set; }
    public int StaffRequired { get; set; }
    public string? ServiceScheduleJson { get; set; }
    public bool IsConfidential { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; } = true;

    public List<EventMilestone> Milestones { get; set; } = [];
    public List<CrewAssignment> CrewAssignments { get; set; } = [];
}

public class EventMilestone
{
    public Guid MilestoneId { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public MilestoneType MilestoneType { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public MilestoneStatus Status { get; set; } = MilestoneStatus.Planned;
}

public class MilestoneDependency
{
    public Guid DependencyId { get; set; } = Guid.NewGuid();
    public Guid PredecessorMilestoneId { get; set; }
    public Guid SuccessorMilestoneId { get; set; }
    public DependencyType DependencyType { get; set; } = DependencyType.FS;
    public int LagHours { get; set; }
}

public class CrewAssignment
{
    public Guid AssignmentId { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public string CrewRole { get; set; } = "";
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public bool Confirmed { get; set; }
    public decimal? HourlyRate { get; set; }
}
