using FluentValidation;
using PushpakClasses.Application.Features.Quizzes.Dtos;

namespace PushpakClasses.Application.Features.Quizzes.Validators;

public class CreateQuizRequestValidator : AbstractValidator<CreateQuizRequestDto>
{
    public CreateQuizRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Quiz title is required.")
            .MaximumLength(250).WithMessage("Quiz title cannot exceed 250 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Quiz description cannot exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Valid category must be selected.");

        RuleFor(x => x.DurationMinutes)
            .GreaterThan(0).WithMessage("Duration must be greater than 0 minutes.");

        RuleFor(x => x.EndAtUtc)
            .GreaterThan(x => x.StartAtUtc).WithMessage("End time (EndAtUtc) must be after start time (StartAtUtc).");

        RuleFor(x => x.PassingPercent)
            .InclusiveBetween(0, 100).WithMessage("Passing percentage must be between 0 and 100.");

        RuleFor(x => x.MaxAttempts)
            .GreaterThanOrEqualTo(1).WithMessage("Maximum attempts must be at least 1.");
    }
}
