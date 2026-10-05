using Carbonate.Application.Common;

namespace Carbonate.Application.Features.Events;

/// <summary>
/// Events, milestones and crew (FR-01, 03, 04, 07, 09, 10). Every method checks the caller's
/// permission itself and answers 404 for an event the caller may not see.
/// </summary>
public interface IEventService
{
    Task<PagedResult<EventListItem>> ListAsync(EventListQuery query, CancellationToken ct);
    Task<EventDetail> GetAsync(Guid eventId, CancellationToken ct);
    Task<EventDetail> CreateAsync(SaveEventRequest request, CancellationToken ct);
    Task<EventDetail> UpdateAsync(Guid eventId, UpdateEventRequest request, CancellationToken ct);
    Task DeleteAsync(Guid eventId, CancellationToken ct);

    Task<IReadOnlyList<MilestoneDto>> GetMilestonesAsync(Guid eventId, CancellationToken ct);
    Task<ScheduleResultDto> RescheduleAsync(Guid eventId, Guid milestoneId, RescheduleRequest request, CancellationToken ct);

    Task<IReadOnlyList<CrewAssignmentDto>> GetCrewAsync(Guid eventId, CancellationToken ct);
    Task<CrewAssignmentDto> AssignCrewAsync(Guid eventId, AssignCrewRequest request, CancellationToken ct);
    Task RemoveCrewAsync(Guid eventId, Guid assignmentId, CancellationToken ct);

    Task<AllowedTransitionsResponse> GetAllowedTransitionsAsync(Guid eventId, CancellationToken ct);
    Task<EventDetail> TransitionAsync(Guid eventId, TransitionRequest request, CancellationToken ct);
}
