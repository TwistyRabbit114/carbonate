using Carbonate.Application.Common;
using Carbonate.Application.Features.Lifecycle;
using Carbonate.Application.Features.Stock;
using Carbonate.Application.Masking;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Features.Events;

namespace Carbonate.Application.Features.Events;

public sealed class EventService(
    IEventRepository events,
    IEventTemplateSeeder templateSeeder,
    IEventLifecycleService lifecycle,
    ITransactionRunner transactions,
    IAuditService audit,
    IFinancialMasker masker,
    ICurrentUser user,
    TimeProvider clock) : IEventService
{
    private const string NotFoundDetail = "That event was not found.";

    /// <summary>Null when the caller may see every event; otherwise their own id, for the assignment check.</summary>
    private Guid? VisibleTo => user.HasPermission(PermissionCodes.EventViewAll) ? null : user.UserId;

    public async Task<PagedResult<EventListItem>> ListAsync(EventListQuery query, CancellationToken ct)
    {
        Require(PermissionCodes.EventViewAssigned);

        query.Page = Math.Max(query.Page, 1);
        query.PageSize = Math.Clamp(query.PageSize, 1, PageQuery.MaxPageSize);

        var page = await events.ListAsync(query, VisibleTo, ct);
        masker.Mask(page, user);
        return page;
    }

    public async Task<EventDetail> GetAsync(Guid eventId, CancellationToken ct)
    {
        Require(PermissionCodes.EventViewAssigned);
        return await DetailAsync(eventId, ct);
    }

    public async Task<EventDetail> CreateAsync(SaveEventRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.EventCreate);
        RequirePriceAccessIfSet(request.BudgetAmount);
        await CheckReferencesAsync(request, null, ct);

        var startsAt = DateTimes.ToUtc(request.StartsAt);
        var endsAt = DateTimes.ToUtc(request.EndsAt);
        var ev = new Event
        {
            EventCode = request.EventCode,
            ClientId = request.ClientId,
            VenueId = request.VenueId,
            DivisionId = request.DivisionId,
            CreatedByUserId = user.UserId,
            Name = request.Name,
            EventType = request.EventType,
            EventDate = request.EventDate,
            HeadcountExpected = request.HeadcountExpected,
            PackSizeEstimated = request.PackSizeEstimated,
            BudgetAmount = request.BudgetAmount,
            PaymentMode = request.PaymentMode,
            InfrastructureMode = request.InfrastructureMode,
            StaffRequired = request.StaffRequired,
            IsConfidential = request.IsConfidential,
            StartsAt = startsAt,
            EndsAt = endsAt,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        var (milestones, dependencies) = MilestoneChain.Build(ev.EventId, startsAt, endsAt);

        // The template seeder reads the milestones to date the cards, so they are saved first. All of it
        // is one transaction: if anything fails, there is no half-built event.
        await transactions.RunAsync(async token =>
        {
            events.Add(ev, milestones, dependencies);
            await events.SaveChangesAsync(token);

            await templateSeeder.SeedAsync(ev.EventId, ev.EventType, ev.DivisionId, ev.PackSizeEstimated, user.UserId, token);
            await events.SaveChangesAsync(token);

            await audit.RecordAsync("event.create", nameof(Event), ev.EventId.ToString(), null, Snapshot(ev), user.UserId, token);
            return ev.EventId;
        }, ct);

        return await DetailAsync(ev.EventId, ct);
    }

    public async Task<EventDetail> UpdateAsync(Guid eventId, UpdateEventRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.EventEdit);

        var ev = await events.FindAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(NotFoundDetail);
        await CheckReferencesAsync(request, eventId, ct);

        var before = Snapshot(ev);

        // Someone who cannot see the budget can neither change nor erase it.
        if (!user.HasPermission(PermissionCodes.FinanceViewClientPrice))
        {
            RequirePriceAccessIfSet(request.BudgetAmount);
        }
        else
        {
            ev.BudgetAmount = request.BudgetAmount;
        }

        events.ExpectRowVersion(ev, RowVersions.Decode(request.RowVersion));

        ev.EventCode = request.EventCode;
        ev.ClientId = request.ClientId;
        ev.VenueId = request.VenueId;
        ev.DivisionId = request.DivisionId;
        ev.Name = request.Name;
        ev.EventType = request.EventType;
        ev.EventDate = request.EventDate;
        ev.HeadcountExpected = request.HeadcountExpected;
        ev.PackSizeEstimated = request.PackSizeEstimated;
        ev.PaymentMode = request.PaymentMode;
        ev.InfrastructureMode = request.InfrastructureMode;
        ev.StaffRequired = request.StaffRequired;
        ev.IsConfidential = request.IsConfidential;
        ev.StartsAt = DateTimes.ToUtc(request.StartsAt);
        ev.EndsAt = DateTimes.ToUtc(request.EndsAt);

        await SaveAsync(eventId, ct);
        await audit.RecordAsync("event.update", nameof(Event), eventId.ToString(), before, Snapshot(ev), user.UserId, ct);

        return await DetailAsync(eventId, ct);
    }

    public async Task<EventDetail> UpdatePackSizeAsync(Guid eventId, PackSizeRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.EventEdit);

        var ev = await events.FindAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(NotFoundDetail);
        var before = new { ev.PackSizeEstimated, ev.PackSizeActual };

        events.ExpectRowVersion(ev, RowVersions.Decode(request.RowVersion));

        // Only what was sent changes, so recording the actual pack size never overwrites the estimate.
        if (request.PackSizeEstimated is { } estimated)
        {
            ev.PackSizeEstimated = estimated;
        }

        if (request.PackSizeActual is { } actual)
        {
            ev.PackSizeActual = actual;
        }

        await SaveAsync(eventId, ct);
        await audit.RecordAsync("event.pack_size", nameof(Event), eventId.ToString(), before,
            new { ev.PackSizeEstimated, ev.PackSizeActual }, user.UserId, ct);

        return await DetailAsync(eventId, ct);
    }

    public async Task DeleteAsync(Guid eventId, CancellationToken ct)
    {
        Require(PermissionCodes.EventDelete);

        var ev = await events.FindAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(NotFoundDetail);
        var before = Snapshot(ev);

        // Records are kept (FR-09). Only the Director can retire one, and it just stops showing.
        ev.IsActive = false;
        await events.SaveChangesAsync(ct);
        await audit.RecordAsync("event.delete", nameof(Event), eventId.ToString(), before, null, user.UserId, ct);
    }

    public async Task<IReadOnlyList<MilestoneDto>> GetMilestonesAsync(Guid eventId, CancellationToken ct)
    {
        Require(PermissionCodes.EventViewAssigned);
        return await events.GetMilestonesAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(NotFoundDetail);
    }

    public async Task<ScheduleResultDto> RescheduleAsync(
        Guid eventId, Guid milestoneId, RescheduleRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.EventEdit);

        var ev = await events.FindAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(NotFoundDetail);
        events.ExpectRowVersion(ev, RowVersions.Decode(request.RowVersion));

        var (milestones, dependencies) = await events.LoadScheduleAsync(eventId, ct);
        var result = ScheduleCalculator.Recalculate(
            [.. milestones.Select(m => new ScheduleItem(m.MilestoneId, m.ScheduledStart, m.ScheduledEnd, m.ActualStart))],
            [.. dependencies.Select(d => new ScheduleLink(d.PredecessorMilestoneId, d.SuccessorMilestoneId, TimeSpan.FromHours(d.LagHours)))],
            milestoneId,
            DateTimes.ToUtc(request.NewStart),
            DateTimes.ToUtc(request.NewEnd));

        if (!result.Succeeded)
        {
            throw result.Failure == ScheduleFailure.UnknownMilestone
                ? ProblemException.NotFound("That milestone was not found on this event.")
                : ProblemException.BusinessRule(result.Message ?? "That change cannot be scheduled.");
        }

        var before = milestones.Where(m => result.Moved.Contains(m.MilestoneId))
            .Select(m => new { m.MilestoneId, m.MilestoneType, m.ScheduledStart, m.ScheduledEnd }).ToList();

        foreach (var item in result.Items.Where(i => result.Moved.Contains(i.Id)))
        {
            var milestone = milestones.Single(m => m.MilestoneId == item.Id);
            milestone.ScheduledStart = item.Start;
            milestone.ScheduledEnd = item.End;
        }

        // Only milestones changed, so touch the event row to move its version on (FR-10).
        events.Touch(ev);
        await SaveAsync(eventId, ct);

        var after = milestones.Where(m => result.Moved.Contains(m.MilestoneId))
            .Select(m => new { m.MilestoneId, m.MilestoneType, m.ScheduledStart, m.ScheduledEnd }).ToList();
        await audit.RecordAsync("event.reschedule", nameof(Event), eventId.ToString(), before, after, user.UserId, ct);

        return new ScheduleResultDto
        {
            Milestones = [.. milestones.Where(m => result.Moved.Contains(m.MilestoneId)).Select(m => ToDto(m, dependencies))],
            RowVersion = RowVersions.Encode(ev.RowVersion),
        };
    }

    public async Task<IReadOnlyList<CrewAssignmentDto>> GetCrewAsync(Guid eventId, CancellationToken ct)
    {
        Require(PermissionCodes.EventViewAssigned);

        var crew = await events.GetCrewAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(NotFoundDetail);
        masker.Mask(crew, user);
        return crew;
    }

    public async Task<PagedResult<ClientOption>> ListClientsAsync(ClientListQuery query, CancellationToken ct)
    {
        RequireEventWriter();

        query.Page = Math.Max(query.Page, 1);
        query.PageSize = Math.Clamp(query.PageSize, 1, PageQuery.MaxPageSize);
        return await events.ListClientsAsync(query, ct);
    }

    public async Task<IReadOnlyList<DivisionOption>> ListDivisionsAsync(CancellationToken ct)
    {
        RequireEventWriter();
        return await events.ListDivisionsAsync(ct);
    }

    public async Task<IReadOnlyList<CrewCandidate>> ListCrewCandidatesAsync(CancellationToken ct)
    {
        Require(PermissionCodes.CrewAssign);
        return await events.ListCrewCandidatesAsync(ct);
    }

    public async Task<CrewAssignmentDto> AssignCrewAsync(Guid eventId, AssignCrewRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.CrewAssign);

        _ = await events.FindAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(NotFoundDetail);

        // Setting a rate is only for people who may see every rate, so it cannot be used to probe or overwrite.
        if (request.HourlyRate is not null && user.ScopeOf(PermissionCodes.FinanceViewStaffCost) != PermissionScope.All)
        {
            throw ProblemException.Forbidden("Only the Director and Accounts can set an hourly rate.");
        }

        if (!await events.UserIsActiveAsync(request.UserId, ct))
        {
            throw ProblemException.Validation(new Dictionary<string, string[]>
            {
                ["userId"] = ["That person does not exist or is not active."],
            });
        }

        var assignment = new CrewAssignment
        {
            EventId = eventId,
            UserId = request.UserId,
            CrewRole = request.CrewRole,
            ShiftStart = DateTimes.ToUtc(request.ShiftStart),
            ShiftEnd = DateTimes.ToUtc(request.ShiftEnd),
            HourlyRate = request.HourlyRate,
        };
        events.AddCrew(assignment);
        await events.SaveChangesAsync(ct);
        await audit.RecordAsync("event.crew_assign", nameof(Event), eventId.ToString(), null,
            new { assignment.AssignmentId, assignment.UserId, assignment.CrewRole, assignment.ShiftStart, assignment.ShiftEnd },
            user.UserId, ct);

        var dto = await events.GetCrewMemberAsync(assignment.AssignmentId, ct)
            ?? throw ProblemException.NotFound("That assignment was not found.");
        masker.Mask(dto, user);
        return dto;
    }

    public async Task RemoveCrewAsync(Guid eventId, Guid assignmentId, CancellationToken ct)
    {
        Require(PermissionCodes.CrewAssign);

        _ = await events.FindAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(NotFoundDetail);
        var assignment = await events.FindCrewAsync(eventId, assignmentId, ct)
            ?? throw ProblemException.NotFound("That assignment was not found.");

        events.RemoveCrew(assignment);
        await events.SaveChangesAsync(ct);
        await audit.RecordAsync("event.crew_remove", nameof(Event), eventId.ToString(),
            new { assignment.AssignmentId, assignment.UserId, assignment.CrewRole }, null, user.UserId, ct);
    }

    public async Task<AllowedTransitionsResponse> GetAllowedTransitionsAsync(Guid eventId, CancellationToken ct)
    {
        Require(PermissionCodes.EventViewAssigned);

        var ev = await events.FindAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(NotFoundDetail);
        return new AllowedTransitionsResponse
        {
            EventId = eventId,
            Current = ev.Status,
            Allowed = await lifecycle.AllowedTransitionsAsync(eventId, ct),
        };
    }

    public async Task<EventDetail> TransitionAsync(Guid eventId, TransitionRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.EventTransition);

        // The lifecycle service does not know who may see what, so visibility is settled here first.
        _ = await events.FindAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(NotFoundDetail);
        await lifecycle.TransitionAsync(eventId, request.To, request.RowVersion, user.UserId, ct);

        return await DetailAsync(eventId, ct);
    }

    private void Require(string permission)
    {
        if (!user.HasPermission(permission))
        {
            throw ProblemException.Forbidden();
        }
    }

    private void RequireEventWriter()
    {
        if (!user.HasPermission(PermissionCodes.EventCreate) && !user.HasPermission(PermissionCodes.EventEdit))
        {
            throw ProblemException.Forbidden();
        }
    }

    private void RequirePriceAccessIfSet(decimal? budget)
    {
        if (budget is not null && !user.HasPermission(PermissionCodes.FinanceViewClientPrice))
        {
            throw ProblemException.Forbidden("You cannot set the budget on an event.");
        }
    }

    private async Task<EventDetail> DetailAsync(Guid eventId, CancellationToken ct)
    {
        var detail = await events.GetDetailAsync(eventId, VisibleTo, ct) ?? throw ProblemException.NotFound(NotFoundDetail);
        masker.Mask(detail, user);
        return detail;
    }

    /// <summary>Saves, and turns a lost race into a 409 carrying the current event so the screen can reload.</summary>
    private async Task SaveAsync(Guid eventId, CancellationToken ct)
    {
        try
        {
            await events.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // The current version is masked before it goes into the response, like any other output.
            var current = await events.GetDetailAsync(eventId, VisibleTo, ct);
            masker.Mask(current, user);
            throw ProblemException.Conflict(
                "/problems/concurrency-conflict",
                "Someone else changed this event.",
                "Reload the event to see their changes, then make yours again.",
                new Dictionary<string, object?> { ["current"] = current });
        }
    }

    private async Task CheckReferencesAsync(SaveEventRequest request, Guid? exceptEventId, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var references = await events.CheckReferencesAsync(request.ClientId, request.VenueId, request.DivisionId, ct);

        if (!references.ClientUsable)
        {
            errors["clientId"] = ["That client does not exist or is not active."];
        }

        if (request.VenueId is null)
        {
            errors["venueId"] = ["Choose a venue."];
        }
        else if (!references.VenueUsable)
        {
            errors["venueId"] = ["That venue does not exist or is not active."];
        }

        if (!references.DivisionExists)
        {
            errors["divisionId"] = ["That division does not exist."];
        }

        if (await events.EventCodeExistsAsync(request.EventCode, exceptEventId, ct))
        {
            errors["eventCode"] = ["That event code is already in use."];
        }

        if (errors.Count > 0)
        {
            throw ProblemException.Validation(errors);
        }
    }

    private static MilestoneDto ToDto(EventMilestone m, IEnumerable<MilestoneDependency> dependencies) => new()
    {
        MilestoneId = m.MilestoneId,
        EventId = m.EventId,
        MilestoneType = m.MilestoneType,
        ScheduledStart = m.ScheduledStart,
        ScheduledEnd = m.ScheduledEnd,
        ActualStart = m.ActualStart,
        ActualEnd = m.ActualEnd,
        Status = m.Status,
        PredecessorIds = [.. dependencies.Where(d => d.SuccessorMilestoneId == m.MilestoneId).Select(d => d.PredecessorMilestoneId)],
    };

    private static object Snapshot(Event ev) => new
    {
        ev.EventCode,
        ev.Name,
        ev.Status,
        ev.ClientId,
        ev.VenueId,
        ev.DivisionId,
        ev.EventType,
        ev.EventDate,
        ev.StartsAt,
        ev.EndsAt,
        ev.PackSizeEstimated,
        ev.BudgetAmount,
        ev.IsActive,
    };
}
