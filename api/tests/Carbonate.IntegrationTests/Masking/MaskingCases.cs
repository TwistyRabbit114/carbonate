using System.Net.Http.Json;
using Carbonate.Application.Platform.Auth;

namespace Carbonate.IntegrationTests.Masking;

//one endpoint that can carry money. Permission is what the endpoint asks for, so the suite knows
//which roles should get in at all, and Expect is the money a finance role must actually see there.
//that positive check stops the suite passing just because an endpoint never sends money
public sealed record MaskingCase(
    string Name,
    string Permission,
    Func<MaskingFixture, string> Path,
    IReadOnlyList<string> Expect,
    Func<MaskingFixture, object>? Body = null)
{
    public Task<HttpResponseMessage> SendAsync(HttpClient client, MaskingFixture fixture) => Body is null
        ? client.GetAsync(Path(fixture))
        : client.PostAsJsonAsync(Path(fixture), Body(fixture));
}

public static class MaskingCases
{
    private static readonly string[] QuoteMoney =
    [
        "subtotalExVat", "vatAmount", "totalIncVat", "internalCostTotal", "marginPercent",
        "targetMarginBand", "unitCostToUs", "unitPriceToClient", "lineTotal",
    ];

    //reconciliation (FR-17) and the native calendar items (FR-39) join this list once they're built.
    //calendar items must carry no money for anyone (FR-42)
    public static readonly IReadOnlyList<MaskingCase> All =
    [
        //the list sends no money today, the budget is on the detail. it stays here so anything added
        //to the list later is checked for the roles that mustn't see it
        new("event list", PermissionCodes.EventViewAssigned,
            _ => "/api/events?pageSize=200", []),
        new("event detail", PermissionCodes.EventViewAssigned,
            f => $"/api/events/{f.EventId}", ["budgetAmount"]),
        new("costings on an event", PermissionCodes.QuoteView,
            f => $"/api/events/{f.EventId}/quotes", QuoteMoney),
        new("one costing", PermissionCodes.QuoteView,
            f => $"/api/quotes/{f.QuoteId}", QuoteMoney),
        new("cost history", PermissionCodes.FinanceViewClientPrice,
            f => $"/api/events/{f.EventId}/cost-history", ["totalIncVat", "internalCostTotal", "marginPercent"]),
        new("invoices", PermissionCodes.InvoiceView,
            _ => "/api/invoices?pageSize=200", ["amountIncVat"]),
        new("stock items", PermissionCodes.StockView,
            _ => "/api/stock/items?pageSize=200", ["standardUnitCost"]),
        new("order lists", PermissionCodes.StockView,
            _ => "/api/order-lists?pageSize=200", ["estimatedUnitCost"]),
        new("one order list", PermissionCodes.StockView,
            f => $"/api/order-lists/{f.OrderListId}", ["estimatedUnitCost"]),
        new("generating order lists", PermissionCodes.OrderGenerate,
            _ => "/api/order-lists/generate", ["estimatedUnitCost"],
            f => new { from = f.OrderFrom.ToString("yyyy-MM-dd"), to = f.OrderTo.ToString("yyyy-MM-dd") }),
        new("incidents on an event", PermissionCodes.IncidentView,
            f => $"/api/events/{f.EventId}/incidents", ["replacementCost"]),
        new("incidents for a piece of equipment", PermissionCodes.IncidentView,
            f => $"/api/incidents?assetId={f.AssetId}", ["replacementCost"]),
    ];

    public static MaskingCase Named(string name) => All.Single(c => c.Name == name);

    //every case for each of these roles, by name so each pair shows as its own test in the run summary
    public static TheoryData<string, string> For(params string[] roles)
    {
        var data = new TheoryData<string, string>();
        foreach (var role in roles)
        {
            foreach (var item in All)
            {
                data.Add(role, item.Name);
            }
        }

        return data;
    }

    public static bool CanReach(string role, MaskingCase item) =>
        RolePermissionMatrix.PermissionsFor(role).Contains(item.Permission);
}
