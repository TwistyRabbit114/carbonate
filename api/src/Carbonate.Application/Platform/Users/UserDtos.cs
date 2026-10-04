using Carbonate.Application.Common;
using Carbonate.Domain.Common;

namespace Carbonate.Application.Platform.Users;

public class UserListItem
{
    public Guid UserId { get; set; }
    public string EmployeeNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public EmploymentType EmploymentType { get; set; }
    public bool IsActive { get; set; }
    public bool MfaEnabled { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
    public DateTime? LastLoginAt { get; set; }
}

public class UserListQuery : PageQuery
{
    public string? Q { get; set; }
    public bool? IsActive { get; set; }
    public string? Role { get; set; }
}

public class CreateUserRequest
{
    public string EmployeeNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public EmploymentType EmploymentType { get; set; }

    /// <summary>Role names, for example Director or CasualCrew. A user may hold several.</summary>
    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>At least 12 characters and not in a known breach.</summary>
    public string InitialPassword { get; set; } = "";
}

/// <summary>Only the fields that are sent change. Changing roles or deactivating ends the user's sessions.</summary>
public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public IReadOnlyList<string>? Roles { get; set; }
    public bool? IsActive { get; set; }
}

public class AuditEntryDto
{
    public long AuditId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string EntityId { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string? IpAddress { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}

public class AuditQuery : PageQuery
{
    public string? Entity { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
