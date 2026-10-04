using Carbonate.Application.Features.Venues;
using FluentValidation;

namespace Carbonate.Api.Features.Venues;

//free text here is plain text, never html. lengths match the columns, and the spa encodes it on render

internal sealed class VenueRequestValidator : AbstractValidator<VenueRequest>
{
    public VenueRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter the venue's name.").MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().WithMessage("Enter the venue's address.").MaximumLength(500);
        RuleFor(x => x.AccessRoute).MaximumLength(2000);
        RuleFor(x => x.LoadingBayDetails).MaximumLength(1000);
        RuleFor(x => x.PpeRequirements).MaximumLength(1000);

        //half an opening time means nothing. the end can be earlier than the start, for a venue open past midnight
        RuleFor(x => x.OperatingHoursEnd)
            .NotNull().When(x => x.OperatingHoursStart is not null)
            .WithMessage("Add the closing time too, or leave both times empty.");
        RuleFor(x => x.OperatingHoursStart)
            .NotNull().When(x => x.OperatingHoursEnd is not null)
            .WithMessage("Add the opening time too, or leave both times empty.");
    }
}

internal sealed class VenueQueryValidator : AbstractValidator<VenueQuery>
{
    public VenueQueryValidator()
    {
        RuleFor(x => x.Q).MaximumLength(200);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200); //plan section 5 caps a page at 200
    }
}

internal sealed class SiteVisitRequestValidator : AbstractValidator<SiteVisitRequest>
{
    public SiteVisitRequestValidator()
    {
        RuleFor(x => x.VisitDate).NotEmpty().WithMessage("Enter the date of the visit.");
        RuleFor(x => x.ConductedByUserId).NotEqual(Guid.Empty).When(x => x.ConductedByUserId is not null);
        RuleFor(x => x.VehicleType).MaximumLength(100);
        RuleFor(x => x.LicencePlate)
            .MaximumLength(20)
            .Matches("^[A-Za-z0-9 -]+$").When(x => !string.IsNullOrWhiteSpace(x.LicencePlate))
            .WithMessage("Use letters, numbers, spaces and dashes only, like CA 123-456.");
        RuleFor(x => x.DriverName).MaximumLength(200);
        RuleFor(x => x.RequiredDriverDetails).MaximumLength(500);
        RuleFor(x => x.CrewNames).MaximumLength(1000);
        RuleFor(x => x.SignInProcedure).MaximumLength(2000);
        RuleFor(x => x.SecurityCheckpoint).MaximumLength(500);
        RuleFor(x => x.HealthSafetyFileRef).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
