using Carbonate.Domain.Common;

namespace Carbonate.Domain.Features.Calendar;

public class CalendarAccount
{
    public Guid AccountId { get; set; } = Guid.NewGuid();
    public Guid ConnectedByUserId { get; set; }
    public string GoogleAccountEmail { get; set; } = "";
    public string GoogleCalendarId { get; set; } = "";
    public string GrantedScope { get; set; } = "";
    public string TokenSecretName { get; set; } = "";
    public DateTime ConnectedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public class CalendarLink
{
    public Guid LinkId { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public CalendarSourceType SourceEntityType { get; set; }
    public Guid SourceEntityId { get; set; }
    public string GoogleEventId { get; set; } = "";
    public DateTime? LastPushedAt { get; set; }
    public string SyncStatus { get; set; } = "";
    public string? LastError { get; set; }
}

public class CalendarOutbox
{
    public Guid OutboxId { get; set; } = Guid.NewGuid();
    public CalendarSourceType SourceEntityType { get; set; }
    public Guid SourceEntityId { get; set; }
    public OutboxOperation Operation { get; set; }
    public DateTime EnqueuedAt { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public string? LastError { get; set; }
}
