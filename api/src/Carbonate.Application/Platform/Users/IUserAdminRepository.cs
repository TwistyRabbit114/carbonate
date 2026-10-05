using Carbonate.Application.Common;
using Carbonate.Domain.Platform;

namespace Carbonate.Application.Platform.Users;

public class CrewOptions
{
    public const string Section = "Crew";

    /// <summary>Days after an event's debrief before a casual crew account is deactivated (FR-36, NFR-21).</summary>
    public int DeactivateAfterDays { get; set; } = 7;
}

public interface IUserAdminRepository
{
    Task<PagedResult<UserListItem>> ListAsync(UserListQuery query, CancellationToken ct);
    Task<UserListItem?> GetAsync(Guid userId, CancellationToken ct);

    /// <summary>The user with their roles, tracked so they can be changed.</summary>
    Task<AppUser?> FindForUpdateAsync(Guid userId, CancellationToken ct);

    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
    Task<bool> EmployeeNumberExistsAsync(string employeeNumber, CancellationToken ct);

    /// <summary>Maps role names to ids. Names that are not roles are simply missing from the result.</summary>
    Task<Dictionary<string, Guid>> RoleIdsAsync(IEnumerable<string> names, CancellationToken ct);

    void Add(AppUser user, IEnumerable<Guid> roleIds, DateTime now);

    /// <summary>Replaces the user's roles with exactly these.</summary>
    void SetRoles(AppUser user, IReadOnlyCollection<Guid> roleIds, DateTime now);

    Task<int> CountActiveDirectorsAsync(CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

public interface IAuditReadRepository
{
    Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, CancellationToken ct);
}
