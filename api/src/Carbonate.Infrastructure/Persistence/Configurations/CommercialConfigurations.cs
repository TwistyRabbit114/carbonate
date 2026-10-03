using Carbonate.Domain.Features.Commercial;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Features.Stock;
using Carbonate.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carbonate.Infrastructure.Persistence.Configurations;

internal class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> b)
    {
        b.ClusterOn(x => x.CreatedAt, x => x.QuoteId);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.EventId, x.Version }).IsUnique();
        b.HasIndex(x => x.CopiedFromQuoteId);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Quote_Totals",
                "[SubtotalExVat] >= 0 AND [VatAmount] >= 0 AND [TotalIncVat] >= 0");
            t.HasCheckConstraint("CK_Quote_Version", "[Version] >= 1");
        });

        b.Ref<Event>(nameof(Quote.EventId), DeleteBehavior.Cascade);
        b.Ref<Quote>(nameof(Quote.CopiedFromQuoteId));
        b.Ref<AppUser>(nameof(Quote.ApprovedByUserId));
        b.HasMany(x => x.Lines).WithOne()
            .HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class QuoteLineConfiguration : IEntityTypeConfiguration<QuoteLine>
{
    public void Configure(EntityTypeBuilder<QuoteLine> b)
    {
        b.HasKey(x => x.QuoteLineId);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.QuoteId);
        b.ToTable(t => t.HasCheckConstraint("CK_QuoteLine_Amounts",
            "[Quantity] >= 0 AND [UnitCostToUs] >= 0 AND [UnitPriceToClient] >= 0"));
    }
}

internal class EventConfirmationConfiguration : IEntityTypeConfiguration<EventConfirmation>
{
    public void Configure(EntityTypeBuilder<EventConfirmation> b)
    {
        b.HasKey(x => x.ConfirmationId);
        b.Property(x => x.ClientPoNumber).HasMaxLength(50);
        b.Property(x => x.DepositReference).HasMaxLength(100);
        b.HasIndex(x => x.EventId);
        b.Ref<Event>(nameof(EventConfirmation.EventId), DeleteBehavior.Cascade);
        b.Ref<Document>(nameof(EventConfirmation.DocumentId));
    }
}

internal class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.ClusterOn(x => x.CreatedAt, x => x.InvoiceId);
        b.Property(x => x.InvoiceNumber).HasMaxLength(30);
        b.HasIndex(x => x.InvoiceNumber).IsUnique();
        b.HasIndex(x => x.EventId);
        b.HasIndex(x => new { x.Status, x.DueDate });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Invoice_Amount", "[AmountIncVat] >= 0");
            t.HasCheckConstraint("CK_Invoice_Dates", "[DueDate] >= [IssuedDate]");
        });
        b.Ref<Event>(nameof(Invoice.EventId), DeleteBehavior.Cascade);
        b.Ref<EventConfirmation>(nameof(Invoice.ConfirmationId));
    }
}

internal class ReconciliationConfiguration : IEntityTypeConfiguration<Reconciliation>
{
    public void Configure(EntityTypeBuilder<Reconciliation> b)
    {
        b.HasKey(x => x.ReconciliationId);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.MarginPercent).HasPrecision(9, 4);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.HasIndex(x => x.EventId).IsUnique();
        b.Ref<Event>(nameof(Reconciliation.EventId), DeleteBehavior.Cascade);
        b.Ref<AppUser>(nameof(Reconciliation.CompletedByUserId));
        b.HasMany(x => x.Lines).WithOne()
            .HasForeignKey(x => x.ReconciliationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class ReconciliationLineConfiguration : IEntityTypeConfiguration<ReconciliationLine>
{
    public void Configure(EntityTypeBuilder<ReconciliationLine> b)
    {
        b.HasKey(x => x.LineId);
        b.HasIndex(x => x.ReconciliationId);
        b.Ref<StockItem>(nameof(ReconciliationLine.StockItemId));
    }
}
