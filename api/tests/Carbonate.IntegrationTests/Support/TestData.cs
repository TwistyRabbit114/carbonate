using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Venues;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;

namespace Carbonate.IntegrationTests.Support;

/// <summary>
/// Builds the smallest valid rows for endpoint tests. Every unique column gets a random suffix, so
/// tests sharing a database never collide and never depend on each other's data.
/// </summary>
public sealed class TestData(CemDbContext db)
{
    public static string Suffix() => Guid.NewGuid().ToString("N")[..8];

    public async Task<AppUser> UserAsync(string fullName = "Test User", bool active = true)
    {
        var suffix = Suffix();
        var user = new AppUser
        {
            EmployeeNumber = $"E{suffix}",
            Email = $"{suffix}@example.test",
            FullName = fullName,
            PasswordHash = "x",
            SecurityStamp = suffix,
            EmploymentType = EmploymentType.Permanent,
            IsActive = active,
        };
        db.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<Venue> VenueAsync(string? name = null, string address = "1 Test Road, Cape Town")
    {
        var venue = new Venue { Name = name ?? $"Venue {Suffix()}", Address = address };
        db.Add(venue);
        await db.SaveChangesAsync();
        return venue;
    }

    //a confirmed event a month out, so the transition worker leaves it alone
    public async Task<Event> EventAsync(Guid createdByUserId, Guid? venueId = null, bool active = true)
    {
        var suffix = Suffix();
        var division = new Division { Code = $"T{suffix[..6]}", Name = $"Division {suffix}" };
        var client = new Client { Name = $"Client {suffix}", PaymentTermsDays = 30 };
        var startsAt = DateTime.UtcNow.Date.AddDays(30).AddHours(16);

        var ev = new Event
        {
            EventCode = $"TST-{suffix[..6]}".ToUpperInvariant(),
            ClientId = client.ClientId,
            DivisionId = division.DivisionId,
            VenueId = venueId,
            CreatedByUserId = createdByUserId,
            Name = $"Event {suffix}",
            EventType = EventType.Corporate,
            Status = EventStatus.ConfirmedInPlanning,
            EventDate = DateOnly.FromDateTime(startsAt),
            PackSizeEstimated = 100,
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(6),
            CreatedAt = DateTime.UtcNow,
            IsActive = active,
        };
        db.AddRange(division, client, ev);
        await db.SaveChangesAsync();
        return ev;
    }

    public async Task AssignAsync(Guid eventId, Guid userId)
    {
        var shiftStart = DateTime.UtcNow.Date.AddDays(30).AddHours(12);
        db.Add(new CrewAssignment
        {
            EventId = eventId,
            UserId = userId,
            CrewRole = "Bartender",
            ShiftStart = shiftStart,
            ShiftEnd = shiftStart.AddHours(10),
        });
        await db.SaveChangesAsync();
    }

    public async Task<SiteVisit> SiteVisitAsync(Guid eventId, Guid conductedByUserId, string? notes = null)
    {
        var visit = new SiteVisit
        {
            EventId = eventId,
            ConductedByUserId = conductedByUserId,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            Notes = notes,
        };
        db.Add(visit);
        await db.SaveChangesAsync();
        return visit;
    }
}
