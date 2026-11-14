using FluentValidation;
using PushpakClasses.Application.Features.Quizzes.Dtos;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Application.Features.Quizzes.Validators;

public class UpdateQuestionRequestValidator : AbstractValidator<UpdateQuestionRequestDto>
{
    public UpdateQuestionRequestValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Question text is required.");

        RuleFor(x => x.Marks)
            .GreaterThan(0).WithMessage("Marks must be greater than 0.");

        RuleFor(x => x.Type)
            .Must(t => t == 0 || Enum.IsDefined(typeof(QuestionType), t))
            .WithMessage("Valid question type is required.");

        RuleFor(x => x.Options)
            .NotNull().WithMessage("Options list cannot be null.")
            .Must(opts => opts != null && opts.Count >= 2).WithMessage("A question must have at least 2 options.");

        RuleForEach(x => x.Options).ChildRules(option =>
        {
            option.RuleFor(o => o.Text)
                .NotEmpty().WithMessage("Option text cannot be empty.");
        });

        RuleFor(x => x)
            .Custom((dto, context) =>
            {
                if (dto.Options == null || dto.Options.Count < 2) return;

                var correctCount = dto.Options.Count(o => o.IsCorrect);
                var effectiveType = dto.Type == 0 ? QuestionType.SingleChoice : dto.Type;

                switch (effectiveType)
                {
                    case QuestionType.SingleChoice:
                        if (correctCount != 1)
                        {
                            context.AddFailure(nameof(dto.Options), "SingleChoice question must have exactly 1 correct option.");
                        }
                        break;

                    case QuestionType.MultiChoice:
                        if (correctCount < 1)
                        {
                            context.AddFailure(nameof(dto.Options), "MultiChoice question must have at least 1 correct option.");
                        }
                        break;

                    case QuestionType.TrueFalse:
                        if (dto.Options.Count != 2)
                        {
                            context.AddFailure(nameof(dto.Options), "True/False question must have exactly 2 options.");
                        }
                        else if (correctCount != 1)
                        {
                            context.AddFailure(nameof(dto.Options), "True/False question must have exactly 1 correct option.");
                        }
                        break;
                }
            });
    }
}
