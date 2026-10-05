namespace Carbonate.Application.Features.Calendar;

public class CalendarConnectionDto
{
    public bool Connected { get; set; }
    public string? GoogleAccountEmail { get; set; }
    public DateTime? ConnectedAt { get; set; }

    /// <summary>True when a push failed with invalid_grant and the calendar must be connected again.</summary>
    public bool ReconnectNeeded { get; set; }

    public DateTime? LastPushedAt { get; set; }
    public string? LastError { get; set; }
}

public class ConnectCalendarResponse
{
    /// <summary>Where to send the browser to grant access. Only the narrow events scope is requested.</summary>
    public string AuthorizationUrl { get; set; } = "";
}
