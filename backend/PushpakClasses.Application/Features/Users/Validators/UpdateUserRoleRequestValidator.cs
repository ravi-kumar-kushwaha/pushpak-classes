using FluentValidation;
using PushpakClasses.Application.Features.Users.Dtos;

namespace PushpakClasses.Application.Features.Users.Validators;

public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequestDto>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Invalid user role specified.");
    }
}
