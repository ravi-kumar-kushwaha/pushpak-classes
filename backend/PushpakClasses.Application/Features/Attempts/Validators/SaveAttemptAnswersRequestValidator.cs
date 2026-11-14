using FluentValidation;
using PushpakClasses.Application.Features.Attempts.Dtos;

namespace PushpakClasses.Application.Features.Attempts.Validators;

public class SaveAttemptAnswersRequestValidator : AbstractValidator<SaveAttemptAnswersRequestDto>
{
    public SaveAttemptAnswersRequestValidator()
    {
        RuleFor(x => x.Answers)
            .NotNull().WithMessage("Answers list cannot be null.");

        RuleForEach(x => x.Answers).ChildRules(answer =>
        {
            answer.RuleFor(a => a.QuestionId)
                .NotEmpty().WithMessage("Question ID is required.");

            answer.RuleFor(a => a.SelectedOptionIds)
                .NotNull().WithMessage("SelectedOptionIds cannot be null.");
        });
    }
}
