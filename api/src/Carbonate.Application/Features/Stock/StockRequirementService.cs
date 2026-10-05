using Carbonate.Application.Common;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Features.Stock;

namespace Carbonate.Application.Features.Stock;

/// <summary>
/// An event's stock plan (FR-26) with the two warnings attached to each line: shortfall (FR-27) and
/// supplier lead time (FR-29).
/// </summary>
public sealed class StockRequirementService(
    IStockRepository stock,
    IAuditService audit,
    ICurrentUser user,
    TimeProvider clock) : IStockRequirementService
{
    public async Task<IReadOnlyList<StockRequirementDto>> GetAsync(Guid eventId, CancellationToken ct)
    {
        Require(PermissionCodes.StockPlan);

        var context = await ContextAsync(eventId, ct);
        var rows = await stock.ListRequirementsAsync(eventId, ct);

        return [.. rows.Select(row => ToDto(row, context))];
    }

    public async Task<IReadOnlyList<StockRequirementDto>> SaveAsync(
        Guid eventId, SaveStockRequirementsRequest request, CancellationToken ct)
    {
        Require(PermissionCodes.StockPlan);

        var context = await ContextAsync(eventId, ct);
        Validate(request);

        var existing = await stock.LoadRequirementsForEditAsync(eventId, ct);
        var kept = new HashSet<Guid>();

        foreach (var line in request.Items)
        {
            var match = line.RequirementId is { } id ? existing.FirstOrDefault(r => r.RequirementId == id) : null;

            if (match is null)
            {
                stock.AddRequirement(new EventStockRequirement
                {
                    EventId = eventId,
                    StockItemId = line.StockItemId,
                    QuantityRequired = line.QuantityRequired,
                    QuantityAllocated = 0,
                    RequiredByDate = line.RequiredByDate,
                    SourceMode = line.SourceMode,
                    Notes = line.Notes,
                });
                continue;
            }

            match.StockItemId = line.StockItemId;
            match.QuantityRequired = line.QuantityRequired;
            match.RequiredByDate = line.RequiredByDate;
            match.SourceMode = line.SourceMode;
            match.Notes = line.Notes;
            kept.Add(match.RequirementId);
        }

        // Anything the client did not send back was removed on screen. QuantityAllocated is deliberately
        // not settable here: stock is allocated by the store, not by the planner.
        var removed = existing.Where(r => !kept.Contains(r.RequirementId)).ToList();
        stock.RemoveRequirements(removed);

        await stock.SaveChangesAsync(ct);
        await audit.RecordAsync("stock.requirements_save", "Event", eventId.ToString(),
            new { Lines = existing.Count }, new { Lines = request.Items.Count }, user.UserId, ct);

        var rows = await stock.ListRequirementsAsync(eventId, ct);
        return [.. rows.Select(row => ToDto(row, context))];
    }

    // ---- helpers ---------------------------------------------------------------------------

    private void Require(string permission)
    {
        if (!user.HasPermission(permission))
        {
            throw ProblemException.Forbidden();
        }
    }

    /// <summary>Also settles visibility: a 404 rather than a 403 so an event's existence is not revealed.</summary>
    private async Task<EventPlanningContext> ContextAsync(Guid eventId, CancellationToken ct)
    {
        var visibleTo = user.HasPermission(PermissionCodes.EventViewAll) ? (Guid?)null : user.UserId;

        return await stock.GetEventContextAsync(eventId, visibleTo, ct)
               ?? throw ProblemException.NotFound("That event was not found.");
    }

    private StockRequirementDto ToDto(RequirementRow row, EventPlanningContext context)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var warnings = new List<StockWarningDto>();

        var shortfall = StockRules.Shortfall(
            row.QuantityRequired, row.ConsumptionPerHundredGuests, context.PackSize, context.LoadInAt, now);
        if (shortfall is not null)
        {
            warnings.Add(ToDto(shortfall));
        }

        // Lead time only matters for something somebody has to be asked for.
        if (OrderListBuilder.IsOrderable(row.SourceMode))
        {
            var leadTime = StockRules.LeadTime(row.RequiredByDate, row.SupplierLeadTimeDays, today);
            if (leadTime is not null)
            {
                warnings.Add(ToDto(leadTime));
            }
        }

        return new StockRequirementDto
        {
            RequirementId = row.RequirementId,
            EventId = row.EventId,
            StockItemId = row.StockItemId,
            StockItemName = row.StockItemName,
            Unit = row.Unit,
            QuantityRequired = row.QuantityRequired,
            QuantityAllocated = row.QuantityAllocated,
            RequiredByDate = row.RequiredByDate,
            SourceMode = row.SourceMode,
            Notes = row.Notes,
            Warnings = warnings,
        };
    }

    internal static StockWarningDto ToDto(StockWarning warning) => new()
    {
        Code = warning.Code,
        Expected = warning.Expected,
        Planned = warning.Planned,
        LeadTimeDays = warning.LeadTimeDays,
        RequiredBy = warning.RequiredBy,
    };

    private static void Validate(SaveStockRequirementsRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        for (var i = 0; i < request.Items.Count; i++)
        {
            var line = request.Items[i];

            if (line.QuantityRequired < 0)
            {
                errors[$"items[{i}].quantityRequired"] = ["A quantity cannot be negative."];
            }

            if (line.StockItemId == Guid.Empty)
            {
                errors[$"items[{i}].stockItemId"] = ["Choose a stock item."];
            }
        }

        // The same item twice would make the shortfall warning compare against half the plan.
        var duplicates = request.Items
            .GroupBy(l => l.StockItemId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            errors["items"] = ["Each stock item can appear only once. Combine the duplicate lines."];
        }

        if (errors.Count > 0)
        {
            throw ProblemException.Validation(errors);
        }
    }
}
