using Carbonate.Application.Features.Boards;
using FluentValidation;

namespace Carbonate.Api.Features.Boards;

//subjects are plain text. descriptions may carry a little formatting and are cleaned in the service, which
//also enforces the 5000 character limit on what's left; this only turns away something absurdly large
internal static class CardRules
{
    public const int MaxRawDescription = 20_000;
    public const int MaxAssignees = 20;

    public static bool BeBase64(string? value) =>
        value is not null && Convert.TryFromBase64String(value, new byte[value.Length], out _);
}

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
            .Must(CardRules.BeBase64).When(x => !string.IsNullOrEmpty(x.RowVersion))
            .WithMessage("rowVersion isn't valid.");
    }
}

internal sealed class CreateCardRequestValidator : AbstractValidator<CreateCardRequest>
{
    public CreateCardRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().WithMessage("Give the card a subject.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(CardRules.MaxRawDescription);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.AssigneeIds).Must(ids => ids is null || ids.Count <= CardRules.MaxAssignees)
            .WithMessage($"A card can have at most {CardRules.MaxAssignees} people on it.");
        RuleForEach(x => x.AssigneeIds).NotEmpty();
    }
}

internal sealed class UpdateCardRequestValidator : AbstractValidator<UpdateCardRequest>
{
    public UpdateCardRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().WithMessage("Give the card a subject.").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(CardRules.MaxRawDescription);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.RowVersion)
            .NotEmpty().WithMessage("Send the card's rowVersion, so a change made meanwhile isn't overwritten.");
        RuleFor(x => x.RowVersion)
            .Must(CardRules.BeBase64).When(x => !string.IsNullOrEmpty(x.RowVersion))
            .WithMessage("rowVersion isn't valid.");
    }
}

internal sealed class CompleteTaskRequestValidator : AbstractValidator<CompleteTaskRequest>
{
    public CompleteTaskRequestValidator()
    {
        RuleFor(x => x.RowVersion)
            .NotEmpty().WithMessage("Send the card's rowVersion, so a change made meanwhile isn't overwritten.");
        RuleFor(x => x.RowVersion)
            .Must(CardRules.BeBase64).When(x => !string.IsNullOrEmpty(x.RowVersion))
            .WithMessage("rowVersion isn't valid.");
    }
}

//notes are required, they're how the assignee knows what to fix (FR-20)
internal sealed class ReturnTaskRequestValidator : AbstractValidator<ReturnTaskRequest>
{
    public ReturnTaskRequestValidator()
    {
        RuleFor(x => x.ReviewNotes)
            .NotEmpty().WithMessage("Say what still needs doing before it comes back.")
            .MaximumLength(2000);
        RuleFor(x => x.RowVersion)
            .NotEmpty().WithMessage("Send the card's rowVersion, so a change made meanwhile isn't overwritten.");
        RuleFor(x => x.RowVersion)
            .Must(CardRules.BeBase64).When(x => !string.IsNullOrEmpty(x.RowVersion))
            .WithMessage("rowVersion isn't valid.");
    }
}

internal sealed class AssigneesRequestValidator : AbstractValidator<AssigneesRequest>
{
    public AssigneesRequestValidator()
    {
        RuleFor(x => x.UserIds).NotNull().WithMessage("Send the full list of people, even if it's empty.");
        RuleFor(x => x.UserIds).Must(ids => ids is null || ids.Count <= CardRules.MaxAssignees)
            .WithMessage($"A card can have at most {CardRules.MaxAssignees} people on it.");
        RuleForEach(x => x.UserIds).NotEmpty();
    }
}
