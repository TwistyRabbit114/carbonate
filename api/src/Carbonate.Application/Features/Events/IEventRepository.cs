using Carbonate.Application.Common;
using Carbonate.Domain.Features.Events;

namespace Carbonate.Application.Features.Events;

/// <summary>
/// Data access for events, milestones and crew. A <c>visibleToUserId</c> of null means the caller may
/// see every event; otherwise only events that user is assigned to are visible. Soft-deleted events
/// are never visible.
/// </summary>
public interface IEventRepository
{
    Task<PagedResult<EventListItem>> ListAsync(EventListQuery query, Guid? visibleToUserId, CancellationToken ct);

    Task<EventDetail?> GetDetailAsync(Guid eventId, Guid? visibleToUserId, CancellationToken ct);

    /// <summary>The tracked event, for changing. Null if it does not exist or is not visible.</summary>
    Task<Event?> FindAsync(Guid eventId, Guid? visibleToUserId, CancellationToken ct);

    Task<bool> EventCodeExistsAsync(string eventCode, Guid? exceptEventId, CancellationToken ct);

    /// <summary>Which of the referenced records exist and are usable.</summary>
    Task<EventReferences> CheckReferencesAsync(Guid clientId, Guid? venueId, Guid divisionId, CancellationToken ct);

    void Add(Event ev, IEnumerable<EventMilestone> milestones, IEnumerable<MilestoneDependency> dependencies);

    /// <summary>Makes the save fail if the stored row version is not this one.</summary>
    void ExpectRowVersion(Event ev, byte[] rowVersion);

    /// <summary>Forces the event row to update, so its row version changes when only children changed.</summary>
    void Touch(Event ev);

    /// <exception cref="ConcurrencyConflictException">Someone else saved first.</exception>
    /// <exception cref="ProblemException">A unique value is already taken.</exception>
    Task SaveChangesAsync(CancellationToken ct);

    Task<IReadOnlyList<MilestoneDto>?> GetMilestonesAsync(Guid eventId, Guid? visibleToUserId, CancellationToken ct);

    /// <summary>The event's milestones and dependencies, tracked so a reschedule can change them.</summary>
    Task<(List<EventMilestone> Milestones, List<MilestoneDependency> Dependencies)> LoadScheduleAsync(
        Guid eventId, CancellationToken ct);

    Task<IReadOnlyList<CrewAssignmentDto>?> GetCrewAsync(Guid eventId, Guid? visibleToUserId, CancellationToken ct);

    Task<CrewAssignmentDto?> GetCrewMemberAsync(Guid assignmentId, CancellationToken ct);

    Task<PagedResult<ClientOption>> ListClientsAsync(ClientListQuery query, CancellationToken ct);

    Task<IReadOnlyList<DivisionOption>> ListDivisionsAsync(CancellationToken ct);

    Task<IReadOnlyList<CrewCandidate>> ListCrewCandidatesAsync(CancellationToken ct);

    Task<bool> UserIsActiveAsync(Guid userId, CancellationToken ct);

    void AddCrew(CrewAssignment assignment);

    Task<CrewAssignment?> FindCrewAsync(Guid eventId, Guid assignmentId, CancellationToken ct);

    void RemoveCrew(CrewAssignment assignment);
}

public sealed record EventReferences(bool ClientUsable, bool VenueUsable, bool DivisionExists);
