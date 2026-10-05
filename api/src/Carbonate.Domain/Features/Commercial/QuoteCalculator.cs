namespace Carbonate.Domain.Features.Commercial;

public sealed record QuoteLineInput(decimal Quantity, decimal UnitCostToUs, decimal UnitPriceToClient);

/// <summary>Everything derived from a quote's lines. Nothing here is trusted from the client.</summary>
public sealed record QuoteTotals(
    IReadOnlyList<decimal> LineTotals,
    decimal SubtotalExVat,
    decimal VatAmount,
    decimal TotalIncVat,
    decimal InternalCostTotal,
    decimal? MarginPercent);

/// <summary>
/// Quote arithmetic (FR-11). Totals are always recomputed on the server and stored, and the VAT rate
/// is passed in from configuration, never written here: the old workbook's bare multiplier was a
/// defect (Task 1 appendix B5).
/// </summary>
public static class QuoteCalculator
{
    public static QuoteTotals Calculate(IReadOnlyList<QuoteLineInput> lines, decimal vatRate)
    {
        // Each line is rounded to the cent first, so the lines always add up to the subtotal shown.
        var lineTotals = lines.Select(l => Money(l.Quantity * l.UnitPriceToClient)).ToList();
        var subtotal = lineTotals.Sum();
        var vat = Money(subtotal * vatRate);
        var internalCost = lines.Sum(l => Money(l.Quantity * l.UnitCostToUs));

        // Margin is profit on what the client pays before VAT, which Carbon passes on to SARS.
        decimal? margin = subtotal > 0 ? Math.Round((subtotal - internalCost) / subtotal * 100m, 2, MidpointRounding.AwayFromZero) : null;

        return new QuoteTotals(lineTotals, subtotal, vat, subtotal + vat, internalCost, margin);
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
