using FluentValidation;
using PushpakClasses.Application.Features.Quizzes.Dtos;

namespace PushpakClasses.Application.Features.Quizzes.Validators;

public class ReorderQuestionsRequestValidator : AbstractValidator<ReorderQuestionsRequestDto>
{
    public ReorderQuestionsRequestValidator()
    {
        RuleFor(x => x.Orders)
            .NotNull().WithMessage("Orders list cannot be null.")
            .NotEmpty().WithMessage("Orders list cannot be empty.");
    }
}
