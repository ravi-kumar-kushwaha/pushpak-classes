using FluentAssertions;
using Hangfire;
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

public class AutoSubmitJobTests
{
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock = new();
    private readonly IRedisService _redisService;

    public AutoSubmitJobTests()
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
    public async Task ProcessExpiredAttemptsAsync_FindsAttemptsPastEndsAtUtcPlusTenSeconds_MarksAutoSubmittedAndEnqueuesScoring()
    {
        // Arrange (Integration test: auto-submit job)
        using var context = CreateDbContext();

        var category = new Category { Id = Guid.NewGuid(), Name = "General Knowledge" };
        await context.Categories.AddAsync(category);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Trivia Quiz",
            CategoryId = category.Id,
            DurationMinutes = 15,
            StartAtUtc = DateTime.UtcNow.AddHours(-1),
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            IsPublished = true,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        var q1Id = Guid.NewGuid();
        var opt1Id = Guid.NewGuid();

        // Expired attempt: ended 20 seconds ago (> 10s buffer)
        var expiredAttempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = Guid.NewGuid(),
            Status = QuizAttemptStatus.InProgress,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-16),
            EndsAtUtc = DateTime.UtcNow.AddSeconds(-20)
        };
        await context.QuizAttempts.AddAsync(expiredAttempt);
        await context.SaveChangesAsync();

        // Put draft answers in Redis
        var redisKey = $"attempt:{expiredAttempt.Id}:answers";
        var draft = new SaveAttemptAnswersRequestDto
        {
            Answers = new List<QuestionAnswerDto>
            {
                new() { QuestionId = q1Id, SelectedOptionIds = new List<Guid> { opt1Id } }
            }
        };
        await _redisService.SetAsync(redisKey, draft);

        var job = new AutoSubmitJob(context, _redisService, _backgroundJobClientMock.Object, NullLogger<AutoSubmitJob>.Instance);

        // Act
        await job.ProcessExpiredAttemptsAsync();

        // Assert
        var dbAttempt = await context.QuizAttempts.FindAsync(expiredAttempt.Id);
        dbAttempt.Should().NotBeNull();
        dbAttempt!.Status.Should().Be(QuizAttemptStatus.AutoSubmitted);
        dbAttempt.SubmittedAtUtc.Should().Be(expiredAttempt.EndsAtUtc);
        dbAttempt.AnswersJson.Should().Contain(q1Id.ToString());

        // Verified background scoring job was enqueued
        _backgroundJobClientMock.Verify(x => x.Create(
            It.IsAny<Hangfire.Common.Job>(),
            It.IsAny<Hangfire.States.IState>()), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredAttemptsAsync_IgnoresAttemptsStillWithinGracePeriod()
    {
        // Arrange
        using var context = CreateDbContext();

        var category = new Category { Id = Guid.NewGuid(), Name = "Geography" };
        await context.Categories.AddAsync(category);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Geography Quiz",
            CategoryId = category.Id,
            DurationMinutes = 10,
            StartAtUtc = DateTime.UtcNow.AddHours(-1),
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            IsPublished = true,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        // Attempt ended only 5 seconds ago (< 10 seconds grace period)
        var activeAttempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = Guid.NewGuid(),
            Status = QuizAttemptStatus.InProgress,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            EndsAtUtc = DateTime.UtcNow.AddSeconds(-5)
        };
        await context.QuizAttempts.AddAsync(activeAttempt);
        await context.SaveChangesAsync();

        var job = new AutoSubmitJob(context, _redisService, _backgroundJobClientMock.Object, NullLogger<AutoSubmitJob>.Instance);

        // Act
        await job.ProcessExpiredAttemptsAsync();

        // Assert
        var dbAttempt = await context.QuizAttempts.FindAsync(activeAttempt.Id);
        dbAttempt!.Status.Should().Be(QuizAttemptStatus.InProgress);

        // No scoring enqueued
        _backgroundJobClientMock.Verify(x => x.Create(
            It.IsAny<Hangfire.Common.Job>(),
            It.IsAny<Hangfire.States.IState>()), Times.Never);
    }
}
