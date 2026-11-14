using FluentValidation;
using PushpakClasses.Application.Features.Reattempts.Dtos;

namespace PushpakClasses.Application.Features.Reattempts.Validators;

public class CreateReattemptRequestValidator : AbstractValidator<CreateReattemptRequestDto>
{
    public CreateReattemptRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MinimumLength(5).WithMessage("Reason must be at least 5 characters.")
            .MaximumLength(1000).WithMessage("Reason cannot exceed 1000 characters.");
    }
}
