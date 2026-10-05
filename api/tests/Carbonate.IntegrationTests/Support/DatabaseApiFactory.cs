using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Carbonate.IntegrationTests.Support;

/// <summary>
/// The real API pipeline over a real SQL Server with every migration applied, for endpoint tests that
/// read and write data. Clients sign in as real users holding a role's real permissions, the same
/// role clients the masking acceptance suite uses.
/// </summary>
public sealed class DatabaseApiFactory : ApiFactory, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _sql.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Sql", _sql.GetConnectionString());
    }

    /// <summary>A context of its own, for arranging data and checking what the API wrote.</summary>
    public CemDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CemDbContext>().UseSqlServer(_sql.GetConnectionString()).Options);

    /// <summary>
    /// A client signed in as this user, holding exactly what the role holds in plan section 7.4. The
    /// user must be saved first: every write is audited, and the audit row points at the user.
    /// </summary>
    public HttpClient ClientFor(AppUser user, string role)
    {
        var permissions = RolePermissionMatrix.PermissionsFor(role);
        var token = Services.GetRequiredService<ITokenService>().CreateAccessToken(user, [role], permissions);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Value);
        return client;
    }
}

/// <summary>Test classes in this collection share one SQL Server container, which takes a while to start.</summary>
[CollectionDefinition(Name)]
public sealed class DatabaseApiCollection : ICollectionFixture<DatabaseApiFactory>
{
    public const string Name = "Database API";
}
