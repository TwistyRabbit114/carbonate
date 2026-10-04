using Carbonate.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Persistence;

internal sealed class TransactionRunner(CemDbContext db) : ITransactionRunner
{
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        // Disposing without a commit rolls everything back, so a failure part-way leaves nothing behind.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await work(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
