using FluentValidation;
using PushpakClasses.Application.Features.Quizzes.Dtos;

namespace PushpakClasses.Application.Features.Quizzes.Validators;

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequestDto>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(150).WithMessage("Category name cannot exceed 150 characters.");
    }
}
