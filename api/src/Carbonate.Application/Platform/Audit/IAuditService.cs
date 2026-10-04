namespace Carbonate.Application.Platform.Audit;

public interface IAuditService
{
    /// <summary>
    /// Appends an audit entry with the actor, time and IP. Call it on every write path. Never pass
    /// passwords, tokens or MFA secrets in <paramref name="before"/> or <paramref name="after"/>.
    /// </summary>
    Task RecordAsync(string action, string entityName, string entityId, object? before, object? after,
        Guid? userId, CancellationToken ct = default);
}
