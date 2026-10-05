using Carbonate.Application.Features.Commercial;
using Carbonate.Domain.Common;
using FluentValidation;

namespace Carbonate.Api.Features.Commercial;

internal sealed class SaveQuoteLineValidator : AbstractValidator<SaveQuoteLine>
{
    public SaveQuoteLineValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.UnitCostToUs).NotNull().WithMessage("Enter what this costs us.")
            .GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000_000);
        RuleFor(x => x.UnitPriceToClient).NotNull().WithMessage("Enter the price to the client.")
            .GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000_000);
    }
}

internal sealed class SaveQuoteRequestValidator : AbstractValidator<SaveQuoteRequest>
{
    public SaveQuoteRequestValidator()
    {
        RuleFor(x => x.Lines).NotNull().Must(l => l.Count <= 500).WithMessage("A costing can have at most 500 lines.");
        RuleForEach(x => x.Lines).SetValidator(new SaveQuoteLineValidator());
        RuleFor(x => x.RowVersion).MaximumLength(100);
    }
}

internal sealed class QuoteActionRequestValidator : AbstractValidator<QuoteActionRequest>
{
    public QuoteActionRequestValidator() => RuleFor(x => x.RowVersion).NotEmpty().MaximumLength(100);
}

internal sealed class CopyQuoteRequestValidator : AbstractValidator<CopyQuoteRequest>
{
    public CopyQuoteRequestValidator() => RuleFor(x => x.SourceQuoteId).NotEmpty();
}

internal sealed class RecordConfirmationRequestValidator : AbstractValidator<RecordConfirmationRequest>
{
    public RecordConfirmationRequestValidator()
    {
        RuleFor(x => x.ConfirmationType).IsInEnum();

        When(x => x.ConfirmationType == ConfirmationType.PurchaseOrder, () =>
        {
            RuleFor(x => x.ClientPoNumber).NotEmpty().WithMessage("Enter the client's PO number.").MaximumLength(50);
            RuleFor(x => x.PoReceivedDate).NotNull().WithMessage("Enter the date the PO was received.");
            RuleFor(x => x.PoAmount).GreaterThanOrEqualTo(0).When(x => x.PoAmount is not null);
        });

        When(x => x.ConfirmationType == ConfirmationType.Deposit, () =>
        {
            RuleFor(x => x.DepositAmount).NotNull().WithMessage("Enter the deposit amount.").GreaterThan(0);
            RuleFor(x => x.DepositPaidDate).NotNull().WithMessage("Enter the date the deposit was paid.");
            RuleFor(x => x.DepositReference).NotEmpty().WithMessage("Enter the payment reference.").MaximumLength(100);
        });
    }
}

internal sealed class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.ConfirmationId).NotEmpty();
        RuleFor(x => x.IssuedDate).NotEqual(default(DateOnly)).WithMessage("Choose the invoice date.");
        RuleFor(x => x.AmountIncVat).GreaterThan(0).LessThanOrEqualTo(1_000_000_000).When(x => x.AmountIncVat is not null);
    }
}

internal sealed class UpdateInvoiceRequestValidator : AbstractValidator<UpdateInvoiceRequest>
{
    public UpdateInvoiceRequestValidator() => RuleFor(x => x.Status).IsInEnum();
}

internal sealed class InvoiceListQueryValidator : AbstractValidator<InvoiceListQuery>
{
    public InvoiceListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort).MaximumLength(30);
    }
}
