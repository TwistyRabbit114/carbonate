namespace Carbonate.Application.Features.Commercial;

public class FinanceOptions
{
    public const string Section = "Finance";

    /// <summary>South African VAT. Configuration, never a constant in code (T1 appendix B5).</summary>
    public decimal VatRate { get; set; } = 0.15m;
}

public class QuoteOptions
{
    public const string Section = "Quotes";

    /// <summary>A quote whose total including VAT is above this needs the Director's approval (FR-14).</summary>
    public decimal ApprovalThresholdZar { get; set; }

    /// <summary>Target margin by pack size (FR-11). Empty means no band is shown.</summary>
    public List<MarginBand> MarginBands { get; set; } = [];
}

public class MarginBand
{
    public int MinPax { get; set; }
    public int MaxPax { get; set; }
    public decimal TargetMinPct { get; set; }
    public decimal TargetMaxPct { get; set; }
}

public static class MarginBands
{
    /// <summary>The band that covers this pack size, or null if none does.</summary>
    public static MarginBand? For(int packSize, IEnumerable<MarginBand> bands) =>
        bands.FirstOrDefault(b => packSize >= b.MinPax && packSize <= b.MaxPax);
}
