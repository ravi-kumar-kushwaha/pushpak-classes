using FluentValidation;
using PushpakClasses.Application.Features.Users.Dtos;

namespace PushpakClasses.Application.Features.Users.Validators;

public class UpdateUserStatusRequestValidator : AbstractValidator<UpdateUserStatusRequestDto>
{
    public UpdateUserStatusRequestValidator()
    {
        // Boolean values are valid for true and false.
    }
}
