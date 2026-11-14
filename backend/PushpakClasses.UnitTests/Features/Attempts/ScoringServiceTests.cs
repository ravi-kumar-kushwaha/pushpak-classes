using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Attempts.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Attempts;

public class ScoringServiceTests
{
    private readonly IRedisService _redisService;

    public ScoringServiceTests()
    {
        var redisOptions = Options.Create(new RedisSettings { Enabled = false });
        _redisService = new RedisService(redisOptions, NullLogger<RedisService>.Instance);
    }

    private PushpakClassesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PushpakClassesDbContext(options);
    }

    [Fact]
    public async Task ScoreAttemptAsync_CalculatesCorrectScoreAndPercentForMixedQuestionTypes()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Student One",
            Email = "student@pushpakclasses.com",
            PasswordHash = "hashed"
        };
        await context.Users.AddAsync(user);

        var category = new Category { Id = Guid.NewGuid(), Name = "Science" };
        await context.Categories.AddAsync(category);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Comprehensive Science Test",
            CategoryId = category.Id,
            DurationMinutes = 60,
            StartAtUtc = DateTime.UtcNow.AddHours(-1),
            EndAtUtc = DateTime.UtcNow.AddHours(2),
            PassingPercent = 60, // Requires >= 60%
            MaxAttempts = 1,
            IsPublished = true,
            CreatedByUserId = user.Id,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        // 1. SingleChoice Question (4 marks)
        var q1 = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is the speed of light approx in vacuum?",
            Type = QuestionType.SingleChoice,
            Marks = 4,
            Order = 1,
            IsDeleted = false
        };
        var q1OptCorrect = new Option { Id = Guid.NewGuid(), QuestionId = q1.Id, Text = "300,000 km/s", IsCorrect = true };
        var q1OptWrong = new Option { Id = Guid.NewGuid(), QuestionId = q1.Id, Text = "150,000 km/s", IsCorrect = false };
        q1.Options.Add(q1OptCorrect);
        q1.Options.Add(q1OptWrong);

        // 2. MultiChoice Question (4 marks)
        var q2 = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "Select all noble gases",
            Type = QuestionType.MultiChoice,
            Marks = 4,
            Order = 2,
            IsDeleted = false
        };
        var q2OptHe = new Option { Id = Guid.NewGuid(), QuestionId = q2.Id, Text = "Helium", IsCorrect = true };
        var q2OptNe = new Option { Id = Guid.NewGuid(), QuestionId = q2.Id, Text = "Neon", IsCorrect = true };
        var q2OptO = new Option { Id = Guid.NewGuid(), QuestionId = q2.Id, Text = "Oxygen", IsCorrect = false };
        q2.Options.Add(q2OptHe);
        q2.Options.Add(q2OptNe);
        q2.Options.Add(q2OptO);

        // 3. TrueFalse Question (2 marks)
        var q3 = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "DNA has a double helix structure.",
            Type = QuestionType.TrueFalse,
            Marks = 2,
            Order = 3,
            IsDeleted = false
        };
        var q3OptTrue = new Option { Id = Guid.NewGuid(), QuestionId = q3.Id, Text = "True", IsCorrect = true };
        var q3OptFalse = new Option { Id = Guid.NewGuid(), QuestionId = q3.Id, Text = "False", IsCorrect = false };
        q3.Options.Add(q3OptTrue);
        q3.Options.Add(q3OptFalse);

        await context.Questions.AddRangeAsync(q1, q2, q3);

        // Total marks = 4 + 4 + 2 = 10 marks
        // Student answers:
        // q1: Correct (4 marks)
        // q2: Incomplete (only selected Helium, not Neon) -> 0 marks
        // q3: Correct True -> 2 marks
        // Total score = 6 / 10 = 60% -> Passed = true!

        var answersDto = new SaveAttemptAnswersRequestDto
        {
            Answers = new List<QuestionAnswerDto>
            {
                new() { QuestionId = q1.Id, SelectedOptionIds = new List<Guid> { q1OptCorrect.Id } },
                new() { QuestionId = q2.Id, SelectedOptionIds = new List<Guid> { q2OptHe.Id } }, // Missing q2OptNe
                new() { QuestionId = q3.Id, SelectedOptionIds = new List<Guid> { q3OptTrue.Id } }
            }
        };

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = user.Id,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(30),
            SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-2),
            AnswersJson = JsonSerializer.Serialize(answersDto)
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var service = new ScoringService(context, _redisService, NullLogger<ScoringService>.Instance);

        // Act
        await service.ScoreAttemptAsync(attempt.Id);

        // Assert
        var updatedAttempt = await context.QuizAttempts
            .Include(a => a.AttemptAnswers)
            .FirstOrDefaultAsync(a => a.Id == attempt.Id);

        updatedAttempt.Should().NotBeNull();
        updatedAttempt!.Score.Should().Be(6m);
        updatedAttempt.TotalMarks.Should().Be(10m);
        updatedAttempt.Percent.Should().Be(60m);
        updatedAttempt.Passed.Should().BeTrue();

        updatedAttempt.AttemptAnswers.Should().HaveCount(3);

        var a1 = updatedAttempt.AttemptAnswers.Single(a => a.QuestionId == q1.Id);
        a1.IsCorrect.Should().BeTrue();
        a1.MarksAwarded.Should().Be(4);

        var a2 = updatedAttempt.AttemptAnswers.Single(a => a.QuestionId == q2.Id);
        a2.IsCorrect.Should().BeFalse();
        a2.MarksAwarded.Should().Be(0);

        var a3 = updatedAttempt.AttemptAnswers.Single(a => a.QuestionId == q3.Id);
        a3.IsCorrect.Should().BeTrue();
        a3.MarksAwarded.Should().Be(2);
    }

    [Fact]
    public async Task ScoreAttemptAsync_TriggersEmailNotificationWhenCompleted()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Student Alert",
            Email = "alert@pushpakclasses.com",
            PasswordHash = "h"
        };
        await context.Users.AddAsync(user);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Notification Quiz",
            CategoryId = Guid.NewGuid(),
            DurationMinutes = 20,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            PassingPercent = 50,
            CreatedByUserId = user.Id,
            IsPublished = true,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        var q = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "Sample?",
            Type = QuestionType.SingleChoice,
            Marks = 10,
            Order = 1,
            IsDeleted = false
        };
        var opt = new Option { Id = Guid.NewGuid(), QuestionId = q.Id, Text = "Ans", IsCorrect = true };
        q.Options.Add(opt);
        await context.Questions.AddAsync(q);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = user.Id,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(10),
            AnswersJson = JsonSerializer.Serialize(new SaveAttemptAnswersRequestDto
            {
                Answers = new List<QuestionAnswerDto> { new() { QuestionId = q.Id, SelectedOptionIds = new List<Guid> { opt.Id } } }
            })
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var emailServiceMock = new Moq.Mock<IEmailService>();
        var service = new ScoringService(context, _redisService, emailServiceMock.Object, NullLogger<ScoringService>.Instance);

        // Act
        await service.ScoreAttemptAsync(attempt.Id);

        // Assert: Email notification called with student's email and quiz title
        emailServiceMock.Verify(e => e.SendQuizResultEmailAsync(
            "alert@pushpakclasses.com",
            "Student Alert",
            "Notification Quiz",
            10m,
            10m,
            100m,
            true,
            attempt.Id,
            It.IsAny<CancellationToken>()), Moq.Times.Once);
    }

    [Fact]
    public async Task ScoreAttemptAsync_ScoresCorrectly_WhenQuestionTypeIsZeroOrDefault()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), FullName = "Test Candidate", Email = "test@pushpakclasses.com", PasswordHash = "hash" };
        var category = new Category { Id = Guid.NewGuid(), Name = "General" };
        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Single Attempt Exam",
            CategoryId = category.Id,
            DurationMinutes = 10,
            StartAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndAtUtc = DateTime.UtcNow.AddMinutes(5),
            PassingPercent = 50,
            CreatedByUserId = user.Id,
            IsPublished = true,
            IsDeleted = false
        };
        await context.Users.AddAsync(user);
        await context.Categories.AddAsync(category);
        await context.Quizzes.AddAsync(quiz);

        // Question with Type = 0 (unspecified / legacy single choice)
        var q = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is 2 + 2?",
            Type = (QuestionType)0,
            Marks = 10,
            Order = 1,
            IsDeleted = false
        };
        var optCorrect = new Option { Id = Guid.NewGuid(), QuestionId = q.Id, Text = "4", IsCorrect = true };
        var optWrong = new Option { Id = Guid.NewGuid(), QuestionId = q.Id, Text = "5", IsCorrect = false };
        q.Options.Add(optCorrect);
        q.Options.Add(optWrong);
        await context.Questions.AddAsync(q);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = user.Id,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(5),
            AnswersJson = JsonSerializer.Serialize(new SaveAttemptAnswersRequestDto
            {
                Answers = new List<QuestionAnswerDto> { new() { QuestionId = q.Id, SelectedOptionIds = new List<Guid> { optCorrect.Id } } }
            })
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var service = new ScoringService(context, _redisService, NullLogger<ScoringService>.Instance);

        // Act
        await service.ScoreAttemptAsync(attempt.Id);

        // Assert: Option "4" selected by student must be awarded full 10 marks and Passed = true
        var updated = await context.QuizAttempts
            .Include(a => a.AttemptAnswers)
            .FirstOrDefaultAsync(a => a.Id == attempt.Id);

        updated.Should().NotBeNull();
        updated!.Score.Should().Be(10m);
        updated.TotalMarks.Should().Be(10m);
        updated.Percent.Should().Be(100m);
        updated.Passed.Should().BeTrue();

        var answer = updated.AttemptAnswers.Single(a => a.QuestionId == q.Id);
        answer.IsCorrect.Should().BeTrue();
        answer.MarksAwarded.Should().Be(10m);
    }

    [Fact]
    public async Task ScoreAttemptAsync_ScoresCorrectly_WhenAnswersJsonEnvelopeHasDraftProperty()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), FullName = "Draft Student", Email = "draft@pushpakclasses.com", PasswordHash = "hash" };
        var category = new Category { Id = Guid.NewGuid(), Name = "General" };
        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Draft Format Quiz",
            CategoryId = category.Id,
            DurationMinutes = 10,
            StartAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndAtUtc = DateTime.UtcNow.AddMinutes(5),
            PassingPercent = 50,
            CreatedByUserId = user.Id,
            IsPublished = true,
            IsDeleted = false
        };
        await context.Users.AddAsync(user);
        await context.Categories.AddAsync(category);
        await context.Quizzes.AddAsync(quiz);

        var q = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is 2 + 2?",
            Type = QuestionType.SingleChoice,
            Marks = 10,
            Order = 1,
            IsDeleted = false
        };
        var optCorrect = new Option { Id = Guid.NewGuid(), QuestionId = q.Id, Text = "4", IsCorrect = true };
        q.Options.Add(optCorrect);
        await context.Questions.AddAsync(q);

        // AnswersJson formatted with ShuffleMap and Draft envelope
        var customEnvelopeJson = JsonSerializer.Serialize(new
        {
            ShuffleMap = new[] { new { QuestionId = q.Id, Order = 1 } },
            Draft = new[] { new { QuestionId = q.Id, SelectedOptionIds = new[] { optCorrect.Id } } }
        });

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = user.Id,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(5),
            AnswersJson = customEnvelopeJson
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var service = new ScoringService(context, _redisService, NullLogger<ScoringService>.Instance);

        // Act
        await service.ScoreAttemptAsync(attempt.Id);

        // Assert: Draft envelope must be parsed successfully, scoring 10/10
        var updated = await context.QuizAttempts
            .Include(a => a.AttemptAnswers)
            .FirstOrDefaultAsync(a => a.Id == attempt.Id);

        updated.Should().NotBeNull();
        updated!.Score.Should().Be(10m);
        updated.TotalMarks.Should().Be(10m);
        updated.Percent.Should().Be(100m);
        updated.Passed.Should().BeTrue();
    }
}
