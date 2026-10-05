using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Carbonate.IntegrationTests.Support;

/// <summary>
/// One SQL Server container for the whole test run. Each fixture gets its own empty database on it, so
/// tests stay isolated without every test class paying for, and holding memory for, its own server.
/// Starting several containers at once exhausts Docker's memory and they exit with code 255.
/// The container is removed when the run ends.
/// </summary>
public static class SharedSqlServer
{
    private static readonly Lazy<Task<MsSqlContainer>> Container = new(async () =>
    {
        var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();
        return container;
    });

    /// <summary>Creates a new empty database and returns a connection string for it.</summary>
    public static async Task<string> CreateDatabaseAsync()
    {
        var container = await Container.Value;
        var name = $"carbonate_{Guid.NewGuid():N}";

        await using (var connection = new SqlConnection(container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{name}]";
            await command.ExecuteNonQueryAsync();
        }

        return new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = name }.ConnectionString;
    }
}
