using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.IntegrationTests.Persistence;

public class SchemaTests(SqlServerFixture sql) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Audit_entries_cannot_be_updated_or_deleted()
    {
        await using var context = sql.CreateContext();
        context.AuditEntries.Add(new AuditEntry
        {
            Action = "event.create",
            EntityName = "Event",
            EntityId = Guid.NewGuid().ToString(),
            OccurredAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var update = () => context.Database.ExecuteSqlRawAsync("UPDATE AuditEntries SET Action = 'tampered'");
        var delete = () => context.Database.ExecuteSqlRawAsync("DELETE FROM AuditEntries");

        await Assert.ThrowsAnyAsync<Exception>(update);
        await Assert.ThrowsAnyAsync<Exception>(delete);
        Assert.Equal(1, await context.AuditEntries.CountAsync(a => a.Action == "event.create"));
    }

    [Fact]
    public async Task Times_come_back_as_utc_so_the_json_carries_a_Z()
    {
        var entityId = Guid.NewGuid().ToString();
        await using (var write = sql.CreateContext())
        {
            write.AuditEntries.Add(new AuditEntry
            {
                Action = "time.check",
                EntityName = "Event",
                EntityId = entityId,
                OccurredAt = DateTime.UtcNow,
            });
            await write.SaveChangesAsync();
        }

        await using var read = sql.CreateContext();
        var entry = await read.AuditEntries.AsNoTracking().SingleAsync(a => a.EntityId == entityId);

        Assert.Equal(DateTimeKind.Utc, entry.OccurredAt.Kind);
        Assert.EndsWith("Z", System.Text.Json.JsonSerializer.Deserialize<string>(
            System.Text.Json.JsonSerializer.Serialize(entry.OccurredAt))!);
    }

    [Theory]
    [InlineData("AB1")]
    [InlineData("TOO-LONG-EVENT-CODE-OVER-20")]
    [InlineData("BAD CODE")]
    [InlineData("bad_code")]
    public async Task Event_code_outside_the_allowlist_is_rejected(string code)
    {
        await using var context = sql.CreateContext();
        var ev = await NewEventAsync(context, code);

        context.Events.Add(ev);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Event_code_must_be_unique()
    {
        await using var context = sql.CreateContext();
        var first = await NewEventAsync(context, "DUP-TEST-26");
        context.Events.Add(first);
        await context.SaveChangesAsync();

        var second = await NewEventAsync(context, "DUP-TEST-26");
        context.Events.Add(second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static async Task<Event> NewEventAsync(DbContext context, string code)
    {
        var client = new Client { Name = "Test client" };
        var division = new Division { Code = Guid.NewGuid().ToString("N")[..8], Name = "Test division" };
        var user = new AppUser
        {
            EmployeeNumber = Guid.NewGuid().ToString("N")[..12],
            Email = $"{Guid.NewGuid():N}@example.test",
            FullName = "Test user",
        };
        context.AddRange(client, division, user);
        await context.SaveChangesAsync();

        var start = DateTime.UtcNow.AddDays(14);
        return new Event
        {
            EventCode = code,
            ClientId = client.ClientId,
            DivisionId = division.DivisionId,
            CreatedByUserId = user.UserId,
            Name = "Test event",
            EventType = EventType.Corporate,
            EventDate = DateOnly.FromDateTime(start),
            PackSizeEstimated = 100,
            PaymentMode = PaymentMode.PurchaseOrder,
            InfrastructureMode = InfrastructureMode.Owned,
            StaffRequired = 4,
            CreatedAt = DateTime.UtcNow,
            StartsAt = start,
            EndsAt = start.AddHours(6),
        };
    }
}
