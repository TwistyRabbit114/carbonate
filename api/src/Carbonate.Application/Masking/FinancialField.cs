namespace Carbonate.Application.Masking;

/// <summary>The four financial tiers from plan section 7.3.</summary>
public enum FinancialTier
{
    /// <summary>Client prices, quote and invoice totals, PO and deposit amounts, budget, revenue.</summary>
    Price,

    /// <summary>What things cost Carbon: unit costs, replacement cost, internal cost totals.</summary>
    Cost,

    /// <summary>Margin percentages, profit and target bands.</summary>
    Margin,

    /// <summary>Hourly rates and shift cost. Director and Accounts see all; everyone else only their own.</summary>
    Staff,
}

/// <summary>
/// Marks a DTO property as a money field in a tier. Any money property belongs to a tier, so tag it.
/// A tagged property must be nullable and must also carry
/// <c>[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]</c>, so a masked value is left out
/// of the JSON instead of being sent as null. A unit test enforces both.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FinancialFieldAttribute(FinancialTier tier) : Attribute
{
    public FinancialTier Tier { get; } = tier;
}
