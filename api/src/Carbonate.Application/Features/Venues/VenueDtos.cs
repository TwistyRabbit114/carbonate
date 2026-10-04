using Carbonate.Application.Common;

namespace Carbonate.Application.Features.Venues;

public class VenueDto
{
    public Guid VenueId { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";

    /// <summary>Plain text. Crew see it only for venues of events they are assigned to.</summary>
    public string? AccessRoute { get; set; }

    public string? LoadingBayDetails { get; set; }
    public TimeOnly? OperatingHoursStart { get; set; }
    public TimeOnly? OperatingHoursEnd { get; set; }
    public bool RequiresSecurityClearance { get; set; }
    public bool RequiresHealthSafetyFile { get; set; }
    public string? PpeRequirements { get; set; }
    public bool IsActive { get; set; }
}

public class VenueListQuery : PageQuery
{
    public string? Q { get; set; }
    public bool? IsActive { get; set; }
}

public class SaveVenueRequest
{
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

public class SiteVisitDto
{
    public Guid SiteVisitId { get; set; }
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

public class SaveSiteVisitRequest
{
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
