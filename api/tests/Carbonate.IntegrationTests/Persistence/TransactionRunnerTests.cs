using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.IntegrationTests.Persistence;

public class TransactionRunnerTests(SqlServerFixture sql) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Work_that_fails_after_saving_is_rolled_back_completely()
    {
        await using var context = sql.CreateContext();
        var runner = new TransactionRunner(context);
        var entityId = Guid.NewGuid().ToString();

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync<int>(async ct =>
        {
            context.AuditEntries.Add(new AuditEntry
            {
                Action = "tx.test",
                EntityName = "Test",
                EntityId = entityId,
                OccurredAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync(ct);
            throw new InvalidOperationException("the seeder failed");
        }, CancellationToken.None));

        await using var fresh = sql.CreateContext();
        Assert.False(await fresh.AuditEntries.AnyAsync(a => a.EntityId == entityId));
    }

    [Fact]
    public async Task Work_that_succeeds_is_committed()
    {
        await using var context = sql.CreateContext();
        var runner = new TransactionRunner(context);
        var entityId = Guid.NewGuid().ToString();

        await runner.RunAsync(async ct =>
        {
            context.AuditEntries.Add(new AuditEntry
            {
                Action = "tx.test",
                EntityName = "Test",
                EntityId = entityId,
                OccurredAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync(ct);
            return 1;
        }, CancellationToken.None);

        await using var fresh = sql.CreateContext();
        Assert.True(await fresh.AuditEntries.AnyAsync(a => a.EntityId == entityId));
    }
}
