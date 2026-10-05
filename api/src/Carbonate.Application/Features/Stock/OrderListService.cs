using Carbonate.Application.Common;
using Carbonate.Application.Masking;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Stock;

namespace Carbonate.Application.Features.Stock;

/// <summary>
/// FR-28 and FR-30. Turns a period's requirements into one draft order per supplier, then walks each
/// list through <c>Draft → PendingApproval → Approved → Placed</c>.
/// </summary>
public sealed class OrderListService(
    IStockRepository stock,
    IAuditService audit,
    IFinancialMasker masker,
    ICurrentUser user,
    TimeProvider clock) : IOrderListService
{
    public async Task<GenerateOrderListsResponse> GenerateAsync(
        GenerateOrderListsRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.OrderGenerate);

        if (request.To < request.From)
        {
            throw ProblemException.Validation(new Dictionary<string, string[]>
            {
                ["to"] = ["The end of the period cannot be before the start."],
            });
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);

        // One query for the whole period, then the grouping happens in memory. NFR-08 allows five
        // seconds for a seeded December, which rules out a query per event or per supplier.
        var candidates = await stock.ListOrderCandidatesAsync(request.From, request.To, ct);
        var generation = OrderListBuilder.Build(candidates, today);

        var created = new List<Guid>();

        foreach (var draft in generation.Drafts)
        {
            var list = new OrderList
            {
                // Null: a consolidated list spans events by design, which is the point of FR-28.
                EventId = null,
                SupplierId = draft.SupplierId,
                GeneratedByUserId = user.UserId,
                Status = OrderListStatus.Draft,
                RequiredByDate = draft.RequiredByDate,
                GeneratedAt = now,
                PeriodStart = request.From,
                PeriodEnd = request.To,
            };

            foreach (var line in draft.Lines)
            {
                list.Lines.Add(new OrderListLine
                {
                    OrderListId = list.OrderListId,
                    StockItemId = line.StockItemId,
                    QuantityOrdered = line.QuantityOrdered,
                    EstimatedUnitCost = line.EstimatedUnitCost,
                });
            }

            stock.AddOrderList(list);
            created.Add(list.OrderListId);
        }

        await stock.SaveChangesAsync(ct);

        // The entity id is the period, not the list ids. AUDIT_ENTRY.EntityId is nvarchar(64), which
        // holds one GUID and not twenty-four — a real December overflowed it. The ids go in the
        // payload, which is nvarchar(max).
        await audit.RecordAsync("order.generate", nameof(OrderList),
            $"{request.From:yyyy-MM-dd}..{request.To:yyyy-MM-dd}",
            null,
            new
            {
                request.From,
                request.To,
                Lists = created.Count,
                Unassigned = generation.Unassigned.Count,
                OrderListIds = created,
            },
            user.UserId, ct);

        var response = new GenerateOrderListsResponse
        {
            OrderLists = [.. await LoadAllAsync(created, ct)],
            // Items with no default supplier are reported, never turned into a list: an order list
            // with no supplier cannot be sent to anyone (FR-28).
            UnassignedSupplier =
            [
                .. generation.Unassigned.Select(u => new UnassignedSupplierWarningDto
                {
                    StockItemId = u.StockItemId,
                    StockItemName = u.ItemName,
                }),
            ],
        };

        masker.Mask(response, user);
        return response;
    }

    public async Task<PagedResult<OrderListDto>> ListAsync(OrderListQuery query, CancellationToken ct)
    {
        Require(PermissionCodes.StockView);

        query.Page = Math.Max(query.Page, 1);
        query.PageSize = Math.Clamp(query.PageSize, 1, PageQuery.MaxPageSize);

        var page = await stock.ListOrderListsAsync(query, ct);
        masker.Mask(page, user);
        return page;
    }

    public async Task<OrderListDto> GetAsync(Guid orderListId, CancellationToken ct)
    {
        Require(PermissionCodes.StockView);
        return await DetailAsync(orderListId, ct);
    }

    public Task<OrderListDto> SubmitAsync(Guid orderListId, OrderListActionRequest request, CancellationToken ct) =>
        MoveAsync(orderListId, OrderListStatus.PendingApproval, PermissionCodes.OrderGenerate, request, ct);

    public Task<OrderListDto> ApproveAsync(Guid orderListId, OrderListActionRequest request, CancellationToken ct) =>
        MoveAsync(orderListId, OrderListStatus.Approved, PermissionCodes.OrderApprove, request, ct);

    public Task<OrderListDto> MarkPlacedAsync(Guid orderListId, OrderListActionRequest request, CancellationToken ct) =>
        MoveAsync(orderListId, OrderListStatus.Placed, PermissionCodes.OrderPlace, request, ct);

    // ---- helpers ---------------------------------------------------------------------------

    private async Task<OrderListDto> MoveAsync(
        Guid orderListId,
        OrderListStatus to,
        string permission,
        OrderListActionRequest request,
        CancellationToken ct)
    {
        Require(permission);

        var list = await stock.FindOrderListAsync(orderListId, ct) ?? throw ProblemException.NotFound();

        if (!OrderListRules.CanMove(list.Status, to))
        {
            throw ProblemException.Conflict(
                "/problems/invalid-transition",
                "Not a valid change.",
                $"An order list cannot move from {list.Status} to {to}.");
        }

        if (to == OrderListStatus.Approved
            && !OrderListRules.CanApprove(list.GeneratedByUserId, user.UserId, approverHasPermission: true))
        {
            // Separation of duties (FR-30). Also a CHECK constraint on the table, so a bug here still
            // cannot write the row.
            throw ProblemException.BusinessRule(
                "The person who generated an order list cannot approve it. Ask someone else with approval rights.");
        }

        stock.ExpectRowVersion(list, RowVersions.Decode(request.RowVersion));

        var from = list.Status;
        var now = clock.GetUtcNow().UtcDateTime;
        list.Status = to;

        if (to == OrderListStatus.Approved)
        {
            list.ApprovedByUserId = user.UserId;
            list.ApprovedAt = now;
        }
        else if (to == OrderListStatus.Placed)
        {
            list.PlacedAt = now;
        }

        await stock.SaveChangesAsync(ct);
        await audit.RecordAsync("order.transition", nameof(OrderList), orderListId.ToString(),
            new { Status = from.ToString() }, new { Status = to.ToString() }, user.UserId, ct);

        return await DetailAsync(orderListId, ct);
    }

    private async Task<OrderListDto> DetailAsync(Guid orderListId, CancellationToken ct)
    {
        var dto = await stock.GetOrderListAsync(orderListId, ct) ?? throw ProblemException.NotFound();
        masker.Mask(dto, user);
        return dto;
    }

    private async Task<IReadOnlyList<OrderListDto>> LoadAllAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var loaded = new List<OrderListDto>();

        foreach (var id in ids)
        {
            if (await stock.GetOrderListAsync(id, ct) is { } dto)
            {
                loaded.Add(dto);
            }
        }

        return loaded;
    }

    private void Require(string permission)
    {
        if (!user.HasPermission(permission))
        {
            throw ProblemException.Forbidden();
        }
    }
}
