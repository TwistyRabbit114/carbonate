using Carbonate.Domain.Common;

namespace Carbonate.Domain.Platform;

public class AppUser
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string EmployeeNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public bool MfaEnabled { get; set; }
    public string? MfaSecretEncrypted { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTime? LockoutEnd { get; set; }

    public List<UserRole> UserRoles { get; set; } = [];
}

public class AppRole
{
    public Guid RoleId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public List<RolePermission> RolePermissions { get; set; } = [];
}

public class Permission
{
    public Guid PermissionId { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
}

public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime GrantedAt { get; set; }

    public AppUser User { get; set; } = null!;
    public AppRole Role { get; set; } = null!;
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public AppRole Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

public class RefreshToken
{
    public Guid TokenId { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string CreatedByIp { get; set; } = "";
    public Guid? ReplacedByTokenId { get; set; }
}

public class Document
{
    public Guid DocumentId { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public string BlobUri { get; set; } = "";
    public string Sha256Hash { get; set; } = "";
    public SensitivityLevel SensitivityLevel { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class AuditEntry
{
    public long AuditId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string EntityId { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string? IpAddress { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
