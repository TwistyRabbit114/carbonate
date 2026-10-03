using System.Text.Json;
using System.Text.Json.Serialization;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;

namespace Carbonate.Infrastructure.Platform.Audit;

internal sealed class AuditService(CemDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAuditService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task RecordAsync(string action, string entityName, string entityId, object? before, object? after,
        Guid? userId, CancellationToken ct = default)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OccurredAt = clock.GetUtcNow().UtcDateTime,
            IpAddress = currentUser.IpAddress,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, Json),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, Json),
        });

        await db.SaveChangesAsync(ct);
    }
}
