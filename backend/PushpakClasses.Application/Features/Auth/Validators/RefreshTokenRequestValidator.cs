using FluentValidation;
using PushpakClasses.Application.Features.Auth.Dtos;

namespace PushpakClasses.Application.Features.Auth.Validators;

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequestDto>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
