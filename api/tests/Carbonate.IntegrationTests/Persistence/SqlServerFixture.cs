using Carbonate.Infrastructure.Persistence;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.IntegrationTests.Persistence;

/// <summary>
/// A real SQL Server database with every migration applied, shared by the tests in a class. The
/// server is shared across the whole run (see <see cref="SharedSqlServer"/>); the database is this fixture's own.
/// </summary>
public class SqlServerFixture : IAsyncLifetime
{
    private string _connectionString = "";

    public CemDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CemDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        return new CemDbContext(options);
    }

    public async Task InitializeAsync()
    {
        _connectionString = await SharedSqlServer.CreateDatabaseAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
