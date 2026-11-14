using FluentAssertions;
using PushpakClasses.Application.Features.Quizzes.Dtos;
using PushpakClasses.Application.Features.Quizzes.Validators;
using PushpakClasses.Domain.Enums;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Quizzes;

public class QuizValidatorTests
{
    private readonly CreateQuizRequestValidator _quizValidator = new();
    private readonly CreateQuestionRequestValidator _questionValidator = new();

    [Fact]
    public void CreateQuizValidator_ValidRequest_PassesValidation()
    {
        var model = new CreateQuizRequestDto
        {
            Title = "Valid Title",
            CategoryId = Guid.NewGuid(),
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(2),
            PassingPercent = 60,
            MaxAttempts = 1
        };

        var result = _quizValidator.Validate(model);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateQuizValidator_InvalidDatesOrDuration_FailsValidation()
    {
        var model = new CreateQuizRequestDto
        {
            Title = "",
            CategoryId = Guid.Empty,
            DurationMinutes = 0,
            StartAtUtc = DateTime.UtcNow.AddHours(3),
            EndAtUtc = DateTime.UtcNow.AddHours(1), // End before Start
            PassingPercent = 150, // Out of range
            MaxAttempts = 0
        };

        var result = _quizValidator.Validate(model);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
        result.Errors.Should().Contain(e => e.PropertyName == "CategoryId");
        result.Errors.Should().Contain(e => e.PropertyName == "DurationMinutes");
        result.Errors.Should().Contain(e => e.PropertyName == "EndAtUtc");
        result.Errors.Should().Contain(e => e.PropertyName == "PassingPercent");
        result.Errors.Should().Contain(e => e.PropertyName == "MaxAttempts");
    }

    [Fact]
    public void CreateQuestionValidator_SingleChoiceRules_EnforcesExactlyOneCorrect()
    {
        var zeroCorrect = new CreateQuestionRequestDto
        {
            Text = "Single choice question",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "A", IsCorrect = false },
                new() { Text = "B", IsCorrect = false }
            }
        };

        var resultZero = _questionValidator.Validate(zeroCorrect);
        resultZero.IsValid.Should().BeFalse();
        resultZero.Errors.Should().Contain(e => e.ErrorMessage.Contains("exactly 1 correct option"));

        var multiCorrect = new CreateQuestionRequestDto
        {
            Text = "Single choice question",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "A", IsCorrect = true },
                new() { Text = "B", IsCorrect = true }
            }
        };

        var resultMulti = _questionValidator.Validate(multiCorrect);
        resultMulti.IsValid.Should().BeFalse();
        resultMulti.Errors.Should().Contain(e => e.ErrorMessage.Contains("exactly 1 correct option"));
    }

    [Fact]
    public void CreateQuestionValidator_TrueFalseRules_EnforcesTwoOptionsAndOneCorrect()
    {
        var threeOptions = new CreateQuestionRequestDto
        {
            Text = "True or false question",
            Type = QuestionType.TrueFalse,
            Marks = 1,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "True", IsCorrect = true },
                new() { Text = "False", IsCorrect = false },
                new() { Text = "Neither", IsCorrect = false }
            }
        };

        var result = _questionValidator.Validate(threeOptions);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("exactly 2 options"));
    }
}
