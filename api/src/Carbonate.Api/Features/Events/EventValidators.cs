using Carbonate.Application.Features.Events;
using Carbonate.Domain.Common;
using FluentValidation;

namespace Carbonate.Api.Features.Events;

internal sealed class SaveEventRequestValidator : AbstractValidator<SaveEventRequest>
{
    public SaveEventRequestValidator()
    {
        // The same allowlist the database check constraint enforces (plan section 5).
        RuleFor(x => x.EventCode).NotEmpty().Length(4, 20)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Use letters, numbers and hyphens only.");
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.DivisionId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EventType).IsInEnum();
        RuleFor(x => x.PaymentMode).IsInEnum();
        RuleFor(x => x.InfrastructureMode).IsInEnum();
        RuleFor(x => x.EventDate).NotEqual(default(DateOnly)).WithMessage("Choose the event date.");
        RuleFor(x => x.StartsAt).NotEqual(default(DateTime)).WithMessage("Choose when the event starts.");
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("The event must end after it starts.");
        RuleFor(x => x.PackSizeEstimated).InclusiveBetween(0, 100_000);
        RuleFor(x => x.StaffRequired).InclusiveBetween(0, 1_000);
        RuleFor(x => x.HeadcountExpected).InclusiveBetween(0, 100_000).When(x => x.HeadcountExpected is not null);
        RuleFor(x => x.BudgetAmount).GreaterThanOrEqualTo(0).When(x => x.BudgetAmount is not null);
    }
}

internal sealed class UpdateEventRequestValidator : AbstractValidator<UpdateEventRequest>
{
    public UpdateEventRequestValidator()
    {
        Include(new SaveEventRequestValidator());
        RuleFor(x => x.RowVersion).NotEmpty().MaximumLength(100);
    }
}

internal sealed class RescheduleRequestValidator : AbstractValidator<RescheduleRequest>
{
    public RescheduleRequestValidator()
    {
        RuleFor(x => x.NewStart).NotEqual(default(DateTime));
        RuleFor(x => x.NewEnd).GreaterThanOrEqualTo(x => x.NewStart).WithMessage("A milestone cannot end before it starts.");
        RuleFor(x => x.RowVersion).NotEmpty().MaximumLength(100);
    }
}

internal sealed class AssignCrewRequestValidator : AbstractValidator<AssignCrewRequest>
{
    public AssignCrewRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CrewRole).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ShiftStart).NotEqual(default(DateTime));
        RuleFor(x => x.ShiftEnd).GreaterThan(x => x.ShiftStart).WithMessage("The shift must end after it starts.");
        RuleFor(x => x.HourlyRate).InclusiveBetween(0, 100_000).When(x => x.HourlyRate is not null);
    }
}

internal sealed class TransitionRequestValidator : AbstractValidator<TransitionRequest>
{
    public TransitionRequestValidator()
    {
        RuleFor(x => x.To).IsInEnum()
            .NotEqual(EventStatus.Enquired).WithMessage("An event cannot move back to Enquired.");
        RuleFor(x => x.RowVersion).NotEmpty().MaximumLength(100);
    }
}

internal sealed class EventListQueryValidator : AbstractValidator<EventListQuery>
{
    public EventListQueryValidator()
    {
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.Sort).MaximumLength(30);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

internal sealed class PackSizeRequestValidator : AbstractValidator<PackSizeRequest>
{
    public PackSizeRequestValidator()
    {
        RuleFor(x => x).Must(x => x.PackSizeEstimated is not null || x.PackSizeActual is not null)
            .WithName("packSize").WithMessage("Send the estimated pack size, the actual pack size, or both.");
        RuleFor(x => x.PackSizeEstimated).InclusiveBetween(0, 100000).When(x => x.PackSizeEstimated is not null);
        RuleFor(x => x.PackSizeActual).InclusiveBetween(0, 100000).When(x => x.PackSizeActual is not null);
        RuleFor(x => x.RowVersion).NotEmpty().MaximumLength(100);
    }
}
