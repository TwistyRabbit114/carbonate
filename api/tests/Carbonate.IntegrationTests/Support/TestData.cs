using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Venues;
using Carbonate.Domain.Platform;
using Carbonate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

    public async Task<EventMilestone> MilestoneAsync(Guid eventId, MilestoneType type = MilestoneType.LoadIn)
    {
        var start = DateTime.UtcNow.Date.AddDays(30).AddHours(6);
        var milestone = new EventMilestone
        {
            EventId = eventId,
            MilestoneType = type,
            ScheduledStart = start,
            ScheduledEnd = start.AddHours(4),
        };
        db.Add(milestone);
        await db.SaveChangesAsync();
        return milestone;
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

    //the admin board is a single board normally seeded on first run, so tests share it and only ever
    //look for their own cards on it
    public async Task<Board> AdminBoardAsync()
    {
        var existing = await db.Boards.Include(b => b.Columns).FirstOrDefaultAsync(b => b.BoardType == BoardType.Admin);
        if (existing is not null)
        {
            return existing;
        }

        var board = new Board { BoardType = BoardType.Admin, Name = "Admin tasks", CreatedAt = DateTime.UtcNow };
        board.Columns.Add(new BoardColumn { Name = "Assigned", Position = 0 });
        board.Columns.Add(new BoardColumn { Name = "In Progress / Needs Review", Position = 1 });
        board.Columns.Add(new BoardColumn { Name = "Complete", Position = 2, IsDoneColumn = true });
        db.Add(board);
        await db.SaveChangesAsync();
        return board;
    }

    public async Task<Board> EventBoardAsync(Guid eventId)
    {
        var board = new Board { BoardType = BoardType.Event, EventId = eventId, Name = "Run sheet", CreatedAt = DateTime.UtcNow };
        board.Columns.Add(new BoardColumn { Name = "To do", Position = 0, WipLimit = 5 });
        board.Columns.Add(new BoardColumn { Name = "Doing", Position = 1 });
        board.Columns.Add(new BoardColumn { Name = "Done", Position = 2, IsDoneColumn = true });
        db.Add(board);
        await db.SaveChangesAsync();
        return board;
    }

    //added at the bottom of the column, like a new card would be
    public async Task<TaskCard> CardAsync(BoardColumn column, Guid createdByUserId, string subject,
        string status = "Open", params Guid[] assignees)
    {
        var card = new TaskCard
        {
            ColumnId = column.ColumnId,
            Subject = subject,
            Status = status,
            Position = await db.TaskCards.CountAsync(c => c.ColumnId == column.ColumnId),
            DueAt = new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
        };
        foreach (var userId in assignees)
        {
            card.Assignments.Add(new TaskAssignment { UserId = userId, AssignedAt = DateTime.UtcNow });
        }
        db.Add(card);
        await db.SaveChangesAsync();
        return card;
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
