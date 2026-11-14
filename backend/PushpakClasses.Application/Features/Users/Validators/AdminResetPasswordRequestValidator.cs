using FluentValidation;
using PushpakClasses.Application.Features.Users.Dtos;

namespace PushpakClasses.Application.Features.Users.Validators;

public class AdminResetPasswordRequestValidator : AbstractValidator<AdminResetPasswordRequestDto>
{
    public AdminResetPasswordRequestValidator()
    {
        RuleFor(x => x.TemporaryPassword)
            .NotEmpty().WithMessage("Temporary password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches(@"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]").WithMessage("Password must contain at least one special character.");
    }
}
