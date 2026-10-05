using System.Text.Json;
using Carbonate.Application.Common;
using Carbonate.Application.Features.Events;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Events;
using Carbonate.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Carbonate.Infrastructure.Features.Events;

internal sealed class EventRepository(CemDbContext db) : IEventRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private static readonly EventStatus[] BoardStatuses =
        [EventStatus.ConfirmedInPlanning, EventStatus.InProgress, EventStatus.Finished];

    /// <summary>
    /// The visibility rule lives in the query, so a hidden event is simply not found: a user may read an
    /// event if they can see every event, or are assigned to it. Retired events are never visible.
    /// </summary>
    private IQueryable<Event> Visible(Guid? userId)
    {
        var query = db.Events.Where(e => e.IsActive);
        return userId is null ? query : query.Where(e => e.CrewAssignments.Any(c => c.UserId == userId));
    }

    public async Task<PagedResult<EventListItem>> ListAsync(EventListQuery query, Guid? visibleToUserId, CancellationToken ct)
    {
        var events = Visible(visibleToUserId);

        if (query.Board)
        {
            events = events.Where(e => BoardStatuses.Contains(e.Status));
        }

        if (query.Status is { } status)
        {
            events = events.Where(e => e.Status == status);
        }

        if (query.From is { } from)
        {
            events = events.Where(e => e.EventDate >= from);
        }

        if (query.To is { } to)
        {
            events = events.Where(e => e.EventDate <= to);
        }

        if (query.ClientId is { } clientId)
        {
            events = events.Where(e => e.ClientId == clientId);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var text = query.Q.Trim();
            events = events.Where(e => e.EventCode.Contains(text) || e.Name.Contains(text));
        }

        var total = await events.CountAsync(ct);
        var rows = await Project(Order(events, query.Sort))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<EventListItem>
        {
            Items = [.. rows.Select(ToListItem)],
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
        };
    }

    public async Task<EventDetail?> GetDetailAsync(Guid eventId, Guid? visibleToUserId, CancellationToken ct)
    {
        var row = await Project(Visible(visibleToUserId).Where(e => e.EventId == eventId)).FirstOrDefaultAsync(ct);
        return row is null ? null : ToDetail(row);
    }

    public Task<Event?> FindAsync(Guid eventId, Guid? visibleToUserId, CancellationToken ct) =>
        Visible(visibleToUserId).FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public Task<bool> EventCodeExistsAsync(string eventCode, Guid? exceptEventId, CancellationToken ct) =>
        db.Events.AnyAsync(e => e.EventCode == eventCode && (exceptEventId == null || e.EventId != exceptEventId), ct);

    public async Task<EventReferences> CheckReferencesAsync(Guid clientId, Guid? venueId, Guid divisionId, CancellationToken ct)
    {
        var client = await db.Clients.AnyAsync(c => c.ClientId == clientId && c.IsActive, ct);
        var venue = venueId is not null
            && await db.Venues.AnyAsync(v => v.VenueId == venueId && v.IsActive, ct);
        var division = await db.Divisions.AnyAsync(d => d.DivisionId == divisionId, ct);

        return new EventReferences(client, venue, division);
    }

    public void Add(Event ev, IEnumerable<EventMilestone> milestones, IEnumerable<MilestoneDependency> dependencies)
    {
        db.Events.Add(ev);
        db.EventMilestones.AddRange(milestones);
        db.MilestoneDependencies.AddRange(dependencies);
    }

    public void ExpectRowVersion(Event ev, byte[] rowVersion) =>
        db.Entry(ev).Property(e => e.RowVersion).OriginalValue = rowVersion;

    public void Touch(Event ev) => db.Entry(ev).Property(e => e.Name).IsModified = true;

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql)
        {
            // A second request took the same code between the check and the save.
            var field = sql.Message.Contains("EventCode", StringComparison.OrdinalIgnoreCase) ? "eventCode" : "value";
            throw ProblemException.Validation(new Dictionary<string, string[]> { [field] = ["That value is already in use."] });
        }
    }

    public async Task<IReadOnlyList<MilestoneDto>?> GetMilestonesAsync(Guid eventId, Guid? visibleToUserId, CancellationToken ct)
    {
        if (!await Visible(visibleToUserId).AnyAsync(e => e.EventId == eventId, ct))
        {
            return null;
        }

        var milestones = await db.EventMilestones.AsNoTracking()
            .Where(m => m.EventId == eventId)
            .OrderBy(m => m.ScheduledStart).ThenBy(m => m.MilestoneId)
            .ToListAsync(ct);
        var links = await db.MilestoneDependencies.AsNoTracking()
            .Where(d => db.EventMilestones.Any(m => m.EventId == eventId && m.MilestoneId == d.SuccessorMilestoneId))
            .ToListAsync(ct);

        return
        [
            .. milestones.Select(m => new MilestoneDto
            {
                MilestoneId = m.MilestoneId,
                EventId = m.EventId,
                MilestoneType = m.MilestoneType,
                ScheduledStart = m.ScheduledStart,
                ScheduledEnd = m.ScheduledEnd,
                ActualStart = m.ActualStart,
                ActualEnd = m.ActualEnd,
                Status = m.Status,
                PredecessorIds = [.. links.Where(d => d.SuccessorMilestoneId == m.MilestoneId).Select(d => d.PredecessorMilestoneId)],
            }),
        ];
    }

    public async Task<(List<EventMilestone> Milestones, List<MilestoneDependency> Dependencies)> LoadScheduleAsync(
        Guid eventId, CancellationToken ct)
    {
        var milestones = await db.EventMilestones.Where(m => m.EventId == eventId).ToListAsync(ct);
        var dependencies = await db.MilestoneDependencies
            .Where(d => db.EventMilestones.Any(m => m.EventId == eventId && m.MilestoneId == d.PredecessorMilestoneId))
            .ToListAsync(ct);

        return (milestones, dependencies);
    }

    public async Task<IReadOnlyList<CrewAssignmentDto>?> GetCrewAsync(Guid eventId, Guid? visibleToUserId, CancellationToken ct)
    {
        if (!await Visible(visibleToUserId).AnyAsync(e => e.EventId == eventId, ct))
        {
            return null;
        }

        return await CrewQuery().Where(c => c.EventId == eventId)
            .OrderBy(c => c.ShiftStart).ThenBy(c => c.FullName)
            .ToListAsync(ct);
    }

    public Task<CrewAssignmentDto?> GetCrewMemberAsync(Guid assignmentId, CancellationToken ct) =>
        CrewQuery().Where(c => c.AssignmentId == assignmentId).FirstOrDefaultAsync(ct);

    public Task<bool> UserIsActiveAsync(Guid userId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.UserId == userId && u.IsActive, ct);

    public void AddCrew(CrewAssignment assignment) => db.CrewAssignments.Add(assignment);

    public Task<CrewAssignment?> FindCrewAsync(Guid eventId, Guid assignmentId, CancellationToken ct) =>
        db.CrewAssignments.FirstOrDefaultAsync(c => c.EventId == eventId && c.AssignmentId == assignmentId, ct);

    public void RemoveCrew(CrewAssignment assignment) => db.CrewAssignments.Remove(assignment);

    private IQueryable<CrewAssignmentDto> CrewQuery() =>
        from c in db.CrewAssignments.AsNoTracking()
        join u in db.Users on c.UserId equals u.UserId
        select new CrewAssignmentDto
        {
            AssignmentId = c.AssignmentId,
            EventId = c.EventId,
            UserId = c.UserId,
            FullName = u.FullName,
            CrewRole = c.CrewRole,
            ShiftStart = c.ShiftStart,
            ShiftEnd = c.ShiftEnd,
            Confirmed = c.Confirmed,
            HourlyRate = c.HourlyRate,
        };

    private static IQueryable<Event> Order(IQueryable<Event> events, string? sort)
    {
        var descending = sort?.StartsWith('-') == true;
        var field = sort?.TrimStart('-').ToLowerInvariant();

        // Only these fields can be sorted on. The id is the tie-break so paging is stable.
        var ordered = (field, descending) switch
        {
            ("eventdate", false) => events.OrderBy(e => e.EventDate),
            ("eventdate", true) => events.OrderByDescending(e => e.EventDate),
            ("name", false) => events.OrderBy(e => e.Name),
            ("name", true) => events.OrderByDescending(e => e.Name),
            ("eventcode", false) => events.OrderBy(e => e.EventCode),
            ("eventcode", true) => events.OrderByDescending(e => e.EventCode),
            ("status", false) => events.OrderBy(e => e.Status),
            ("status", true) => events.OrderByDescending(e => e.Status),
            ("createdat", false) => events.OrderBy(e => e.CreatedAt),
            ("createdat", true) => events.OrderByDescending(e => e.CreatedAt),
            ("startsat", true) => events.OrderByDescending(e => e.StartsAt),
            _ => events.OrderBy(e => e.StartsAt),
        };

        return ordered.ThenBy(e => e.EventId);
    }

    private IQueryable<EventRow> Project(IQueryable<Event> events) =>
        events.AsNoTracking().Select(e => new EventRow
        {
            Event = e,
            DivisionCode = db.Divisions.Where(d => d.DivisionId == e.DivisionId).Select(d => d.Code).First(),
            VenueName = db.Venues.Where(v => v.VenueId == e.VenueId).Select(v => v.Name).FirstOrDefault(),
            ClientName = db.Clients.Where(c => c.ClientId == e.ClientId).Select(c => c.Name).First(),
        });

    private static EventListItem ToListItem(EventRow row) => Fill(new EventListItem(), row);

    private static EventDetail ToDetail(EventRow row)
    {
        var e = row.Event;
        var detail = Fill(new EventDetail(), row);
        detail.ClientId = e.ClientId;
        detail.ClientName = row.ClientName;
        detail.VenueId = e.VenueId;
        detail.DivisionId = e.DivisionId;
        detail.CreatedByUserId = e.CreatedByUserId;
        detail.HeadcountExpected = e.HeadcountExpected;
        detail.HeadcountConfirmed = e.HeadcountConfirmed;
        detail.PaymentMode = e.PaymentMode;
        detail.InfrastructureMode = e.InfrastructureMode;
        detail.StaffRequired = e.StaffRequired;
        detail.ServiceSchedule = string.IsNullOrWhiteSpace(e.ServiceScheduleJson)
            ? null
            : JsonDocument.Parse(e.ServiceScheduleJson).RootElement.Clone();
        detail.BudgetAmount = e.BudgetAmount;
        detail.CreatedAt = e.CreatedAt;
        detail.IsActive = e.IsActive;
        return detail;
    }

    private static T Fill<T>(T item, EventRow row) where T : EventListItem
    {
        var e = row.Event;
        item.EventId = e.EventId;
        item.EventCode = e.EventCode;
        item.Name = e.Name;
        item.Status = e.Status;
        item.EventType = e.EventType;
        item.DivisionCode = row.DivisionCode;
        item.EventDate = e.EventDate;
        item.StartsAt = e.StartsAt;
        item.EndsAt = e.EndsAt;
        item.VenueName = row.VenueName;
        item.PackSizeEstimated = e.PackSizeEstimated;
        item.PackSizeActual = e.PackSizeActual;
        item.IsConfidential = e.IsConfidential;
        item.RowVersion = RowVersions.Encode(e.RowVersion);
        return item;
    }

    private sealed class EventRow
    {
        public Event Event { get; set; } = null!;
        public string DivisionCode { get; set; } = "";
        public string? VenueName { get; set; }
        public string ClientName { get; set; } = "";
    }
}
