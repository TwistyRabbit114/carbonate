using Carbonate.Application.Platform.Auth;
using FluentValidation;

namespace Carbonate.Api.Platform.Auth;

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

internal sealed class MfaVerifyRequestValidator : AbstractValidator<MfaVerifyRequest>
{
    public MfaVerifyRequestValidator()
    {
        RuleFor(x => x.MfaToken).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{6}$").WithMessage("Enter the 6-digit code from your app.");
    }
}

internal sealed class MfaConfirmRequestValidator : AbstractValidator<MfaConfirmRequest>
{
    public MfaConfirmRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{6}$").WithMessage("Enter the 6-digit code from your app.");
    }
}
