using Carbonate.Application.Features.Boards;
using FluentValidation;

namespace Carbonate.Api.Features.Boards;

internal sealed class MoveCardRequestValidator : AbstractValidator<MoveCardRequest>
{
    public MoveCardRequestValidator()
    {
        RuleFor(x => x.ColumnId).NotEmpty();
        RuleFor(x => x.Position).GreaterThanOrEqualTo(0);
        //two rules, because a When on one chain would switch off the NotEmpty check too
        RuleFor(x => x.RowVersion)
            .NotEmpty().WithMessage("Send the card's rowVersion, so a change made meanwhile isn't overwritten.");
        RuleFor(x => x.RowVersion)
            .Must(BeBase64).When(x => !string.IsNullOrEmpty(x.RowVersion))
            .WithMessage("rowVersion isn't valid.");
    }

    private static bool BeBase64(string? value) =>
        value is not null && Convert.TryFromBase64String(value, new byte[value.Length], out _);
}
