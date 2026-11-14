using FluentValidation;
using PushpakClasses.Application.Features.Auth.Dtos;

namespace PushpakClasses.Application.Features.Auth.Validators;

public class LogoutRequestValidator : AbstractValidator<LogoutRequestDto>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
