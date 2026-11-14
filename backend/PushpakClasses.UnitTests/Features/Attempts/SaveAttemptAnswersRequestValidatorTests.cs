using FluentAssertions;
using PushpakClasses.Application.Features.Attempts.Dtos;
using PushpakClasses.Application.Features.Attempts.Validators;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Attempts;

public class SaveAttemptAnswersRequestValidatorTests
{
    private readonly SaveAttemptAnswersRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidAnswersList_PassesValidation()
    {
        var model = new SaveAttemptAnswersRequestDto
        {
            Answers = new List<QuestionAnswerDto>
            {
                new()
                {
                    QuestionId = Guid.NewGuid(),
                    SelectedOptionIds = new List<Guid> { Guid.NewGuid() }
                }
            }
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyQuestionId_FailsValidation()
    {
        var model = new SaveAttemptAnswersRequestDto
        {
            Answers = new List<QuestionAnswerDto>
            {
                new()
                {
                    QuestionId = Guid.Empty, // Invalid empty Guid
                    SelectedOptionIds = new List<Guid> { Guid.NewGuid() }
                }
            }
        };

        var result = _validator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Question ID is required"));
    }
}
