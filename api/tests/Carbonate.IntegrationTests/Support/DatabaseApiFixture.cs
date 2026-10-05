using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Venues;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Carbonate.Infrastructure.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Carbonate.IntegrationTests.Support;

/// <summary>The real API pipeline in front of a real SQL Server with every migration applied and the platform seed run.</summary>
public sealed class DbApiFactory(string connectionString) : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Sql", connectionString);
    }
}

public sealed class DatabaseApiFixture : IAsyncLifetime
{
    public DbApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = await SharedSqlServer.CreateDatabaseAsync();

        // Migrate and seed before the host starts, so its background workers find a ready database.
        var options = new DbContextOptionsBuilder<CemDbContext>().UseSqlServer(connectionString).Options;
        await using (var db = new CemDbContext(options))
        {
            await db.Database.MigrateAsync();
            await new PlatformSeeder(db).SeedAsync();
        }

        Factory = new DbApiFactory(connectionString);
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}

[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseApiFixture>
{
    public const string Name = "Database";
}

/// <summary>Creates users, tokens and reference data for a test, each with unique values so tests never collide.</summary>
public sealed class Scenario(DbApiFactory factory)
{
    public sealed record ReferenceData(Guid ClientId, Guid VenueId, Guid DivisionId);

    public async Task<AppUser> UserAsync(params string[] roles)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CemDbContext>();

        var unique = Guid.NewGuid().ToString("N");
        var user = new AppUser
        {
            EmployeeNumber = unique[..12],
            Email = $"{unique}@example.test",
            FullName = $"Test {roles.FirstOrDefault() ?? "User"}",
            PasswordHash = "not-a-real-hash",
            EmploymentType = Domain.Common.EmploymentType.Permanent,
        };
        db.Users.Add(user);

        var roleIds = await db.Roles.Where(r => roles.Contains(r.Name)).ToListAsync();
        foreach (var role in roleIds)
        {
            db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId, GrantedAt = DateTime.UtcNow });
        }

        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>A client signed in as this user, holding the permissions their roles give.</summary>
    public HttpClient ClientFor(AppUser user, params string[] roles)
    {
        var permissions = roles.SelectMany(RolePermissionMatrix.PermissionsFor).Distinct().ToList();
        var token = factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(user, roles, permissions);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Value);
        return client;
    }

    public async Task<(AppUser User, HttpClient Client)> SignedInAsync(string role)
    {
        var user = await UserAsync(role);
        return (user, ClientFor(user, role));
    }

    public async Task<ReferenceData> ReferenceDataAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CemDbContext>();

        var client = new Client { Name = $"Client {Guid.NewGuid():N}"[..20], PaymentTermsDays = 30 };
        var venue = new Venue { Name = $"Venue {Guid.NewGuid():N}"[..20], Address = "1 Test Street, Cape Town" };
        db.Add(client);
        db.Add(venue);
        await db.SaveChangesAsync();

        var division = await db.Divisions.FirstAsync(d => d.Code == "CE");
        return new ReferenceData(client.ClientId, venue.VenueId, division.DivisionId);
    }

    public async Task<T> WithDbAsync<T>(Func<CemDbContext, Task<T>> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<CemDbContext>());
    }

    public Task WithDbAsync(Func<CemDbContext, Task> work) =>
        WithDbAsync<object?>(async db =>
        {
            await work(db);
            return null;
        });

    /// <summary>The body of a valid create request. Override fields by editing the returned dictionary.</summary>
    public static Dictionary<string, object?> NewEventBody(ReferenceData reference, string? code = null)
    {
        var starts = DateTime.UtcNow.AddDays(30).Date.AddHours(17);
        return new Dictionary<string, object?>
        {
            ["eventCode"] = code ?? $"T-{Guid.NewGuid():N}"[..14],
            ["clientId"] = reference.ClientId,
            ["venueId"] = reference.VenueId,
            ["divisionId"] = reference.DivisionId,
            ["name"] = "Integration test event",
            ["eventType"] = "Corporate",
            ["eventDate"] = DateOnly.FromDateTime(starts).ToString("yyyy-MM-dd"),
            ["startsAt"] = starts,
            ["endsAt"] = starts.AddHours(6),
            ["packSizeEstimated"] = 120,
            ["paymentMode"] = "PurchaseOrder",
            ["infrastructureMode"] = "Owned",
            ["staffRequired"] = 6,
            ["isConfidential"] = false,
            ["budgetAmount"] = 85000.00m,
        };
    }
}
