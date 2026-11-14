using FluentValidation;
using PushpakClasses.Application.Features.Auth.Dtos;

namespace PushpakClasses.Application.Features.Auth.Validators;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}
