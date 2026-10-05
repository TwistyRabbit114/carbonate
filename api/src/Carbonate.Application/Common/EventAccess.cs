namespace Carbonate.Application.Common;

/// <summary>
/// The read rule every event-scoped record inherits (plan section 7.2): the caller holds
/// <c>event.view_all</c> or has a crew assignment on the event, and the event hasn't been deleted.
/// Anything that fails this is reported as 404, never 403.
/// </summary>
public interface IEventAccess
{
    Task<bool> CanSeeEventAsync(Guid eventId, CancellationToken ct);
}
