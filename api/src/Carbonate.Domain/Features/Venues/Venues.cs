namespace Carbonate.Domain.Features.Venues;

public class Venue
{
    public Guid VenueId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string? AccessRoute { get; set; }
    public string? LoadingBayDetails { get; set; }
    public TimeOnly? OperatingHoursStart { get; set; }
    public TimeOnly? OperatingHoursEnd { get; set; }
    public bool RequiresSecurityClearance { get; set; }
    public bool RequiresHealthSafetyFile { get; set; }
    public string? PpeRequirements { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SiteVisit
{
    public Guid SiteVisitId { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid ConductedByUserId { get; set; }
    public DateOnly VisitDate { get; set; }
    public string? VehicleType { get; set; }
    public string? SignInProcedure { get; set; }
    public string? SecurityCheckpoint { get; set; }
    public string? RequiredDriverDetails { get; set; }
    public string? Notes { get; set; }
    public string? LicencePlate { get; set; }
    public string? DriverName { get; set; }
    public string? CrewNames { get; set; }
    public string? HealthSafetyFileRef { get; set; }
}
