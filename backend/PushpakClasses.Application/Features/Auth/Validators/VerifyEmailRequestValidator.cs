using FluentValidation;
using PushpakClasses.Application.Features.Auth.Dtos;

namespace PushpakClasses.Application.Features.Auth.Validators;

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequestDto>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Verification token is required.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("A valid email address is required.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
