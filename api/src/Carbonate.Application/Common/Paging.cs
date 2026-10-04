namespace Carbonate.Application.Common;

/// <summary>Every list endpoint answers in this shape (plan section 5).</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int Total { get; init; }
}

/// <summary>Query parameters shared by list endpoints. The page size defaults to 50 and is capped at 200.</summary>
public class PageQuery
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>A field name, optionally prefixed with - for descending, for example <c>-eventDate</c>.</summary>
    public string? Sort { get; set; }
}
