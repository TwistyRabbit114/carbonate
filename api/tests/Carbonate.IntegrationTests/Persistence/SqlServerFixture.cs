using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Carbonate.IntegrationTests.Persistence;

/// <summary>A real SQL Server with every migration applied, shared by the tests in a class.</summary>
public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public CemDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CemDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;
        return new CemDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
