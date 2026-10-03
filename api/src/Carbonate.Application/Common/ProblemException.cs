namespace Carbonate.Application.Common;

/// <summary>
/// A failure the API reports as RFC 7807 problem details. Services throw these; the API's
/// exception handler turns them into the status code and body the client sees.
/// </summary>
public class ProblemException : Exception
{
    private ProblemException(int status, string type, string title, string detail)
        : base(detail)
    {
        Status = status;
        Type = type;
        Title = title;
    }

    public int Status { get; }
    public string Type { get; }
    public string Title { get; }
    public IDictionary<string, string[]> Errors { get; private init; } = new Dictionary<string, string[]>();
    public IDictionary<string, object?> Extensions { get; private init; } = new Dictionary<string, object?>();

    public static ProblemException Validation(IDictionary<string, string[]> errors) =>
        new(400, "/problems/validation", "One or more fields are invalid.", "Check the highlighted fields and try again.")
        {
            Errors = errors,
        };

    public static ProblemException Unauthenticated(string detail) =>
        new(401, "/problems/unauthenticated", "Sign in required.", detail);

    public static ProblemException Forbidden(string detail = "You do not have permission to do that.") =>
        new(403, "/problems/forbidden", "Not allowed.", detail);

    /// <summary>Also used for objects the caller may not see, so their existence is not revealed.</summary>
    public static ProblemException NotFound(string detail = "That record was not found.") =>
        new(404, "/problems/not-found", "Not found.", detail);

    public static ProblemException Conflict(string type, string title, string detail,
        IDictionary<string, object?>? extensions = null) =>
        new(409, type, title, detail) { Extensions = extensions ?? new Dictionary<string, object?>() };

    public static ProblemException BusinessRule(string detail) =>
        new(422, "/problems/business-rule", "That is not allowed right now.", detail);
}
