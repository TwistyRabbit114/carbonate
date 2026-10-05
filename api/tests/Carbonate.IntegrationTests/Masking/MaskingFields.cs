namespace Carbonate.IntegrationTests.Masking;

//every money field the api can send, by tier, as it appears in the json. this is the list the suite
//checks responses against, so a new money field goes in here in the same PR that adds it.
//Every_money_field_in_the_api_is_on_this_list fails if one is missing (plan section 7.3)
public static class MaskingFields
{
    public static readonly IReadOnlySet<string> Price = new HashSet<string>(StringComparer.Ordinal)
    {
        "budgetAmount",
        "subtotalExVat",
        "vatAmount",
        "totalIncVat",
        "unitPriceToClient",
        "lineTotal",
        "poAmount",
        "depositAmount",
        "amountIncVat",
        "revenueFromClient",
        "lineRevenue",
    };

    public static readonly IReadOnlySet<string> Cost = new HashSet<string>(StringComparer.Ordinal)
    {
        "internalCostTotal",
        "unitCostToUs",
        "standardUnitCost",
        "estimatedUnitCost",
        "replacementCost",
        "directCostToUs",
        "stockVarianceValue",
    };

    public static readonly IReadOnlySet<string> Margin = new HashSet<string>(StringComparer.Ordinal)
    {
        "marginPercent",
        "profitRetained",
        "lineProfit",
        "targetMarginBand",
        "targetMinPct",
        "targetMaxPct",
    };

    //staff cost has the self rule, so it's checked row by row rather than with the other three
    public static readonly IReadOnlySet<string> Staff = new HashSet<string>(StringComparer.Ordinal)
    {
        "hourlyRate",
    };

    //price, cost and margin always travel together (T1 section 7.6), so one set covers all three
    public static readonly IReadOnlySet<string> Finance =
        new HashSet<string>(Price.Concat(Cost).Concat(Margin), StringComparer.Ordinal);
}
