using Carbonate.Application.Platform.Auth;
using Carbonate.Application.Platform.Users;
using FluentValidation;

namespace Carbonate.Api.Platform.Users;

internal sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.EmployeeNumber).NotEmpty().MaximumLength(30)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Use letters, numbers and hyphens only.");
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Choose at least one role.")
            .Must(r => r.Count <= RoleNames.All.Count).WithMessage("Too many roles.");
        RuleForEach(x => x.Roles).NotEmpty().MaximumLength(50);
        RuleFor(x => x.InitialPassword).NotEmpty().MaximumLength(128);
    }
}

internal sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x).Must(x => x.FullName is not null || x.Roles is not null || x.IsActive is not null)
            .WithName("body").WithMessage("Send at least one thing to change.");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200).When(x => x.FullName is not null);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Choose at least one role.").When(x => x.Roles is not null);
        RuleForEach(x => x.Roles).NotEmpty().MaximumLength(50).When(x => x.Roles is not null);
    }
}

internal sealed class UserListQueryValidator : AbstractValidator<UserListQuery>
{
    public UserListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.Role).MaximumLength(50);
        RuleFor(x => x.Sort).MaximumLength(30);
    }
}

internal sealed class AuditQueryValidator : AbstractValidator<AuditQuery>
{
    public AuditQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Entity).MaximumLength(100);
        RuleFor(x => x.Sort).MaximumLength(30);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("The end date cannot be before the start date.");
    }
}
