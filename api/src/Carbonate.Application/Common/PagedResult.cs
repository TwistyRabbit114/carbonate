namespace Carbonate.Application.Common;

/// <summary>
/// The shape every list endpoint answers in (plan section 5): <c>{ items, page, pageSize, total }</c>.
/// </summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
