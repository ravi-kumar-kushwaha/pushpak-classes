using FluentValidation;
using PushpakClasses.Application.Features.Users.Dtos;

namespace PushpakClasses.Application.Features.Users.Validators;

public class AdminUpdateUserRequestValidator : AbstractValidator<AdminUpdateUserRequestDto>
{
    public AdminUpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(200).WithMessage("Full name must not exceed 200 characters.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

        RuleFor(x => x.ProfileImageUrl)
            .MaximumLength(500).WithMessage("Profile image URL must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.ProfileImageUrl));
    }
}
