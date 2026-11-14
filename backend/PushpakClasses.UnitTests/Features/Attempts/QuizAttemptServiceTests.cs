using System.Net;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Attempts.Dtos;
using PushpakClasses.Application.Features.Reattempts.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Attempts;

public class QuizAttemptServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IBackgroundJobClient> _backgroundJobClientMock = new();
    private readonly IRedisService _redisService;
    private readonly Guid _userId = Guid.NewGuid();

    public QuizAttemptServiceTests()
    {
        _currentUserMock.Setup(u => u.UserId).Returns(_userId);
        _currentUserMock.Setup(u => u.Role).Returns("User");

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

    private async Task<(Quiz Quiz, Question Q1, Question Q2)> SeedQuizWithQuestionsAsync(PushpakClassesDbContext context)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Mathematics" };
        await context.Categories.AddAsync(category);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Math Final Exam",
            Description = "Algebra and Calculus",
            CategoryId = category.Id,
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow.AddMinutes(-10),
            EndAtUtc = DateTime.UtcNow.AddHours(2),
            PassingPercent = 60,
            MaxAttempts = 2,
            ShuffleQuestions = true,
            ShuffleOptions = true,
            IsPublished = true,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        var q1 = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is 5 x 5?",
            Type = QuestionType.SingleChoice,
            Marks = 2,
            Order = 1,
            IsDeleted = false
        };
        q1.Options.Add(new Option { Id = Guid.NewGuid(), QuestionId = q1.Id, Text = "20", IsCorrect = false });
        q1.Options.Add(new Option { Id = Guid.NewGuid(), QuestionId = q1.Id, Text = "25", IsCorrect = true });

        var q2 = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is 10 / 2?",
            Type = QuestionType.SingleChoice,
            Marks = 3,
            Order = 2,
            IsDeleted = false
        };
        q2.Options.Add(new Option { Id = Guid.NewGuid(), QuestionId = q2.Id, Text = "5", IsCorrect = true });
        q2.Options.Add(new Option { Id = Guid.NewGuid(), QuestionId = q2.Id, Text = "6", IsCorrect = false });

        await context.Questions.AddRangeAsync(q1, q2);
        await context.SaveChangesAsync();

        return (quiz, q1, q2);
    }

    [Fact]
    public async Task GetAvailableQuizzesAsync_ReturnsQuizzesWithinScheduleWithAttemptsRemaining()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, _, _) = await SeedQuizWithQuestionsAsync(context);

        // Add 1 prior submitted attempt for the user
        await context.QuizAttempts.AddAsync(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = DateTime.UtcNow.AddHours(-1),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(-30),
            SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-30)
        });
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act
        var result = await service.GetAvailableQuizzesAsync(new QuizQueryParameters { PageNumber = 1, PageSize = 10 });

        // Assert
        result.Items.Should().HaveCount(1);
        var item = result.Items[0];
        item.Id.Should().Be(quiz.Id);
        item.MaxAttempts.Should().Be(2);
        item.AttemptsUsed.Should().Be(1);
        item.AttemptsRemaining.Should().Be(1);
        item.HasActiveAttempt.Should().BeFalse();
    }

    [Fact]
    public async Task StartQuizAttemptAsync_ValidQuiz_CreatesAttemptAndReturnsSanitizedQuestionsWithoutIsCorrect()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, _, _) = await SeedQuizWithQuestionsAsync(context);
        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act
        var response = await service.StartQuizAttemptAsync(quiz.Id);

        // Assert
        response.Should().NotBeNull();
        response.QuizId.Should().Be(quiz.Id);
        response.QuizTitle.Should().Be("Math Final Exam");
        response.DurationMinutes.Should().Be(30);
        response.EndsAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(2));
        response.Questions.Should().HaveCount(2);

        // Verify questions and options do not leak IsCorrect
        foreach (var q in response.Questions)
        {
            q.Options.Should().NotBeEmpty();
            foreach (var opt in q.Options)
            {
                opt.Text.Should().NotBeNullOrEmpty();
                // AttemptOptionDto does not have IsCorrect property at all
            }
        }

        // Verify attempt persisted in DB
        var dbAttempt = await context.QuizAttempts.FindAsync(response.AttemptId);
        dbAttempt.Should().NotBeNull();
        dbAttempt!.Status.Should().Be(QuizAttemptStatus.InProgress);
        dbAttempt.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task StartQuizAttemptAsync_TimerCappedByQuizEndAtUtc()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, _, _) = await SeedQuizWithQuestionsAsync(context);

        // Quiz ends in 15 minutes, but DurationMinutes is 30 -> EndsAtUtc must be capped to quiz.EndAtUtc
        var quizEnd = DateTime.UtcNow.AddMinutes(15);
        quiz.EndAtUtc = quizEnd;
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act
        var response = await service.StartQuizAttemptAsync(quiz.Id);

        // Assert
        response.EndsAtUtc.Should().BeCloseTo(quizEnd, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StartQuizAttemptAsync_WhenUserHasInProgressAttempt_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, _, _) = await SeedQuizWithQuestionsAsync(context);

        // Add active attempt
        await context.QuizAttempts.AddAsync(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.InProgress,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(25)
        });
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act & Assert
        var act = () => service.StartQuizAttemptAsync(quiz.Id);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("active quiz attempt in progress");
    }

    [Fact]
    public async Task StartQuizAttemptAsync_WhenMaxAttemptsReached_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, _, _) = await SeedQuizWithQuestionsAsync(context);
        quiz.MaxAttempts = 1;

        await context.QuizAttempts.AddAsync(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = DateTime.UtcNow.AddHours(-2),
            EndsAtUtc = DateTime.UtcNow.AddHours(-1)
        });
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act & Assert
        var act = () => service.StartQuizAttemptAsync(quiz.Id);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("maximum allowed attempts");
    }

    [Fact]
    public async Task StartQuizAttemptAsync_CachesQuestionsInRedisForTenMinutes()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, _, _) = await SeedQuizWithQuestionsAsync(context);
        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        var redisKey = $"quiz:{quiz.Id}:questions";
        var beforeCache = await _redisService.GetAsync<List<CachedQuestionDto>>(redisKey);
        beforeCache.Should().BeNull();

        // Act
        await service.StartQuizAttemptAsync(quiz.Id);

        // Assert
        var cached = await _redisService.GetAsync<List<CachedQuestionDto>>(redisKey);
        cached.Should().NotBeNull();
        cached.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveDraftAnswersAsync_ValidTime_StoresAnswersInRedis()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, q1, _) = await SeedQuizWithQuestionsAsync(context);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.InProgress,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(25)
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        var selectedOptId = q1.Options.First().Id;
        var request = new SaveAttemptAnswersRequestDto
        {
            Answers = new List<QuestionAnswerDto>
            {
                new() { QuestionId = q1.Id, SelectedOptionIds = new List<Guid> { selectedOptId } }
            }
        };

        // Act
        var result = await service.SaveDraftAnswersAsync(attempt.Id, request);

        // Assert
        result.Message.Should().Contain("Draft answers saved successfully");

        var redisKey = $"attempt:{attempt.Id}:answers";
        var savedInRedis = await _redisService.GetAsync<SaveAttemptAnswersRequestDto>(redisKey);
        savedInRedis.Should().NotBeNull();
        savedInRedis!.Answers.Should().HaveCount(1);
        savedInRedis.Answers[0].QuestionId.Should().Be(q1.Id);
        savedInRedis.Answers[0].SelectedOptionIds.Should().Contain(selectedOptId);
    }

    [Fact]
    public async Task SaveDraftAnswersAsync_AfterExpiryPlusFiveSeconds_ThrowsBadRequest()
    {
        // Arrange (Integration test: save after expiry)
        using var context = CreateDbContext();
        var (quiz, q1, _) = await SeedQuizWithQuestionsAsync(context);

        // Attempt expired 6 seconds ago (past the 5s grace period)
        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.InProgress,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-31),
            EndsAtUtc = DateTime.UtcNow.AddSeconds(-6)
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        var request = new SaveAttemptAnswersRequestDto
        {
            Answers = new List<QuestionAnswerDto>
            {
                new() { QuestionId = q1.Id, SelectedOptionIds = new List<Guid> { q1.Options.First().Id } }
            }
        };

        // Act & Assert
        var act = () => service.SaveDraftAnswersAsync(attempt.Id, request);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task SaveDraftAnswersAsync_WhenAlreadySubmitted_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, q1, _) = await SeedQuizWithQuestionsAsync(context);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(10),
            SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        var request = new SaveAttemptAnswersRequestDto
        {
            Answers = new List<QuestionAnswerDto>
            {
                new() { QuestionId = q1.Id, SelectedOptionIds = new List<Guid> { q1.Options.First().Id } }
            }
        };

        // Act & Assert
        var act = () => service.SaveDraftAnswersAsync(attempt.Id, request);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("already been submitted");
    }

    [Fact]
    public async Task SubmitQuizAttemptAsync_FlushesAnswersFromRedisAndEnqueuesScoring()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, q1, _) = await SeedQuizWithQuestionsAsync(context);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.InProgress,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(20)
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        // Store draft in Redis
        var redisKey = $"attempt:{attempt.Id}:answers";
        var draft = new SaveAttemptAnswersRequestDto
        {
            Answers = new List<QuestionAnswerDto>
            {
                new() { QuestionId = q1.Id, SelectedOptionIds = new List<Guid> { q1.Options.First(o => o.IsCorrect).Id } }
            }
        };
        await _redisService.SetAsync(redisKey, draft);

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act
        var result = await service.SubmitQuizAttemptAsync(attempt.Id);

        // Assert
        result.Status.Should().Be("Submitted");
        result.Message.Should().Contain("submitted successfully");

        var dbAttempt = await context.QuizAttempts.FindAsync(attempt.Id);
        dbAttempt!.Status.Should().Be(QuizAttemptStatus.Submitted);
        dbAttempt.SubmittedAtUtc.Should().NotBeNull();
        dbAttempt.AnswersJson.Should().Contain(q1.Id.ToString());

        // Background scoring enqueued
        _backgroundJobClientMock.Verify(x => x.Create(
            It.IsAny<Hangfire.Common.Job>(),
            It.IsAny<Hangfire.States.IState>()), Times.Once);
    }

    [Fact]
    public async Task SubmitQuizAttemptAsync_DoubleSubmit_IsIdempotentAndDoesNotThrowOrRescore()
    {
        // Arrange (Integration test: double submit)
        using var context = CreateDbContext();
        var (quiz, _, _) = await SeedQuizWithQuestionsAsync(context);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.InProgress,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(20)
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act 1: First submit
        var result1 = await service.SubmitQuizAttemptAsync(attempt.Id);

        // Act 2: Second submit
        var result2 = await service.SubmitQuizAttemptAsync(attempt.Id);

        // Assert: Both succeed idempotently
        result1.Status.Should().Be("Submitted");
        result2.Status.Should().Be("Submitted");
        result2.Message.Should().Contain("already been submitted");

        // Scoring job only enqueued ONCE
        _backgroundJobClientMock.Verify(x => x.Create(
            It.IsAny<Hangfire.Common.Job>(),
            It.IsAny<Hangfire.States.IState>()), Times.Once);
    }

    [Fact]
    public async Task RecordTabSwitchAsync_IncrementsTabSwitchCount()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, _, _) = await SeedQuizWithQuestionsAsync(context);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.InProgress,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(25),
            TabSwitchCount = 0
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act 1
        var res1 = await service.RecordTabSwitchAsync(attempt.Id);
        // Act 2
        var res2 = await service.RecordTabSwitchAsync(attempt.Id);

        // Assert
        res1.TabSwitchCount.Should().Be(1);
        res2.TabSwitchCount.Should().Be(2);

        var dbAttempt = await context.QuizAttempts.FindAsync(attempt.Id);
        dbAttempt!.TabSwitchCount.Should().Be(2);
    }

    [Fact]
    public async Task StartQuizAttemptAsync_BeforeScheduleStart_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, _, _) = await SeedQuizWithQuestionsAsync(context);
        quiz.StartAtUtc = DateTime.UtcNow.AddHours(2);
        quiz.EndAtUtc = DateTime.UtcNow.AddHours(4);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act & Assert
        var act = () => service.StartQuizAttemptAsync(quiz.Id);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("has not started yet");
    }

    [Fact]
    public async Task StartQuizAttemptAsync_AfterScheduleEnd_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, _, _) = await SeedQuizWithQuestionsAsync(context);
        quiz.StartAtUtc = DateTime.UtcNow.AddHours(-3);
        quiz.EndAtUtc = DateTime.UtcNow.AddHours(-1);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act & Assert
        var act = () => service.StartQuizAttemptAsync(quiz.Id);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("has ended");
    }

    [Fact]
    public async Task GetAttemptResultAsync_ReturnsCorrectAttemptResultDto()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, _, _) = await SeedQuizWithQuestionsAsync(context);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(30),
            SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            Score = 15,
            TotalMarks = 20,
            Percent = 75,
            Passed = true,
            TabSwitchCount = 1
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act
        var result = await service.GetAttemptResultAsync(attempt.Id);

        // Assert
        result.Should().NotBeNull();
        result.AttemptId.Should().Be(attempt.Id);
        result.QuizTitle.Should().Be("Math Final Exam");
        result.Score.Should().Be(15);
        result.TotalMarks.Should().Be(20);
        result.Percent.Should().Be(75);
        result.Passed.Should().BeTrue();
        result.TabSwitchCount.Should().Be(1);
    }

    [Fact]
    public async Task GetActiveAttemptAsync_ShouldReturnActiveAttemptWithQuestionsAndDraft()
    {
        // Arrange
        using var context = CreateDbContext();
        var (quiz, q1, _) = await SeedQuizWithQuestionsAsync(context);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.InProgress,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(50),
            Score = 0,
            TotalMarks = 20,
            Percent = 0,
            Passed = false,
            TabSwitchCount = 2,
            AnswersJson = "{}"
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act
        var result = await service.GetActiveAttemptAsync(attempt.Id);

        // Assert
        result.Should().NotBeNull();
        result.AttemptId.Should().Be(attempt.Id);
        result.QuizTitle.Should().Be("Math Final Exam");
        result.Status.Should().Be("InProgress");
        result.TabSwitchCount.Should().Be(2);
        result.Questions.Should().HaveCount(2);
        result.Questions.First().Options.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAvailableQuizzesAsync_FiltersQuizzesByScheduleAndPublishedStatus()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Science" };
        await context.Categories.AddAsync(category);

        var now = DateTime.UtcNow;

        // 1. Open quiz (within schedule, published, not deleted)
        var openQuiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Active Chemistry Exam",
            CategoryId = category.Id,
            DurationMinutes = 20,
            StartAtUtc = now.AddHours(-1),
            EndAtUtc = now.AddHours(2),
            PassingPercent = 50,
            MaxAttempts = 3,
            IsPublished = true,
            IsDeleted = false
        };

        // 2. Upcoming quiz
        var upcomingQuiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Upcoming Physics Exam",
            CategoryId = category.Id,
            DurationMinutes = 20,
            StartAtUtc = now.AddHours(2),
            EndAtUtc = now.AddHours(4),
            PassingPercent = 50,
            MaxAttempts = 1,
            IsPublished = true,
            IsDeleted = false
        };

        // 3. Closed quiz
        var closedQuiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Closed Biology Exam",
            CategoryId = category.Id,
            DurationMinutes = 20,
            StartAtUtc = now.AddHours(-5),
            EndAtUtc = now.AddHours(-1),
            PassingPercent = 50,
            MaxAttempts = 1,
            IsPublished = true,
            IsDeleted = false
        };

        // 4. Unpublished quiz
        var unpubQuiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Draft Botany Exam",
            CategoryId = category.Id,
            DurationMinutes = 20,
            StartAtUtc = now.AddHours(-1),
            EndAtUtc = now.AddHours(1),
            PassingPercent = 50,
            MaxAttempts = 1,
            IsPublished = false,
            IsDeleted = false
        };

        await context.Quizzes.AddRangeAsync(openQuiz, upcomingQuiz, closedQuiz, unpubQuiz);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act: without status filter, all published quizzes returned with computed status
        var resultAll = await service.GetAvailableQuizzesAsync(new QuizQueryParameters { PageNumber = 1, PageSize = 10 });

        // Assert: unpubQuiz is excluded, 3 published quizzes returned with computed status
        resultAll.Items.Should().HaveCount(3);
        resultAll.Items.Should().Contain(q => q.Id == openQuiz.Id && q.Status == "Open");
        resultAll.Items.Should().Contain(q => q.Id == upcomingQuiz.Id && q.Status == "Upcoming");
        resultAll.Items.Should().Contain(q => q.Id == closedQuiz.Id && q.Status == "Expired");

        // Act: with Open status filter
        var resultOpen = await service.GetAvailableQuizzesAsync(new QuizQueryParameters { PageNumber = 1, PageSize = 10, Status = "Open" });
        resultOpen.Items.Should().HaveCount(1);
        resultOpen.Items[0].Id.Should().Be(openQuiz.Id);
        resultOpen.Items[0].Title.Should().Be("Active Chemistry Exam");
        resultOpen.Items[0].Status.Should().Be("Open");
        resultOpen.Items[0].AttemptsLeft.Should().Be(3);
    }

    [Fact]
    public async Task GetAvailableQuizzesAsync_FiltersByCategoryAndSearchKeyword()
    {
        // Arrange
        using var context = CreateDbContext();
        var catA = new Category { Id = Guid.NewGuid(), Name = "Category A" };
        var catB = new Category { Id = Guid.NewGuid(), Name = "Category B" };
        await context.Categories.AddRangeAsync(catA, catB);

        var now = DateTime.UtcNow;
        var quizA1 = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Geometry Basics",
            CategoryId = catA.Id,
            DurationMinutes = 15,
            StartAtUtc = now.AddHours(-1),
            EndAtUtc = now.AddHours(1),
            IsPublished = true,
            IsDeleted = false
        };
        var quizA2 = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Algebra Advanced",
            CategoryId = catA.Id,
            DurationMinutes = 15,
            StartAtUtc = now.AddHours(-1),
            EndAtUtc = now.AddHours(1),
            IsPublished = true,
            IsDeleted = false
        };
        var quizB = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "History Overview",
            CategoryId = catB.Id,
            DurationMinutes = 15,
            StartAtUtc = now.AddHours(-1),
            EndAtUtc = now.AddHours(1),
            IsPublished = true,
            IsDeleted = false
        };
        await context.Quizzes.AddRangeAsync(quizA1, quizA2, quizB);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // Act & Assert Category filter
        var catFilterResult = await service.GetAvailableQuizzesAsync(new QuizQueryParameters { CategoryId = catA.Id, PageNumber = 1, PageSize = 10 });
        catFilterResult.Items.Should().HaveCount(2);

        // Act & Assert Search filter
        var searchResult = await service.GetAvailableQuizzesAsync(new QuizQueryParameters { SearchQuery = "Geometry", PageNumber = 1, PageSize = 10 });
        searchResult.Items.Should().HaveCount(1);
        searchResult.Items[0].Title.Should().Be("Geometry Basics");
    }

    [Fact]
    public async Task CreateReattemptRequest_ValidationAndApproval_Flow()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "IT" };
        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Certification Quiz",
            CategoryId = category.Id,
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow.AddHours(-1),
            EndAtUtc = DateTime.UtcNow.AddHours(2),
            MaxAttempts = 1,
            IsPublished = true,
            IsDeleted = false
        };
        var user = new User
        {
            Id = _userId,
            Email = "student@pushpakclasses.com",
            FullName = "Test Student",
            Role = UserRole.User,
            PasswordHash = "hash"
        };
        await context.Categories.AddAsync(category);
        await context.Quizzes.AddAsync(quiz);
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(context, _currentUserMock.Object, _redisService, _backgroundJobClientMock.Object);

        // 1. Cannot request reattempt before attempting (attemptsUsed == 0 < maxAttempts 1)
        var preAttemptAct = async () => await service.CreateReattemptRequestAsync(quiz.Id, new CreateReattemptRequestDto { Reason = "Please let me reattempt" });
        await preAttemptAct.Should().ThrowAsync<AuthException>()
            .WithMessage("*remaining attempts*");

        // 2. Add 1 attempt to exhaust MaxAttempts (1 of 1)
        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(-10),
            Status = QuizAttemptStatus.Submitted,
            Score = 40,
            Percent = 40,
            Passed = false
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        // 3. User requests reattempt
        var reattemptDto = await service.CreateReattemptRequestAsync(quiz.Id, new CreateReattemptRequestDto { Reason = "Power outage occurred during test" });
        reattemptDto.Should().NotBeNull();
        reattemptDto.Status.Should().Be("Pending");
        reattemptDto.Reason.Should().Be("Power outage occurred during test");

        // 4. Duplicate pending request is blocked
        var duplicateAct = async () => await service.CreateReattemptRequestAsync(quiz.Id, new CreateReattemptRequestDto { Reason = "Second request" });
        await duplicateAct.Should().ThrowAsync<AuthException>()
            .WithMessage("*pending reattempt request*");

        // 5. Admin reviews and approves request
        var reviewResult = await service.ReviewReattemptRequestAsync(reattemptDto.Id, new ReviewReattemptRequestDto
        {
            Status = ReattemptRequestStatus.Approved,
            ReviewNotes = "Granted 1 extra attempt due to verified power outage."
        });
        reviewResult.Status.Should().Be("Approved");
        reviewResult.ReviewNotes.Should().Be("Granted 1 extra attempt due to verified power outage.");

        // 6. Available quizzes now reflects allowedAttempts = 2, attemptsRemaining = 1, status = Open
        var availResult = await service.GetAvailableQuizzesAsync(new QuizQueryParameters { PageNumber = 1, PageSize = 10 });
        var targetQuiz = availResult.Items.FirstOrDefault(q => q.Id == quiz.Id);
        targetQuiz.Should().NotBeNull();
        targetQuiz!.AllowedAttempts.Should().Be(2);
        targetQuiz.AttemptsUsed.Should().Be(1);
        targetQuiz.AttemptsRemaining.Should().Be(1);
        targetQuiz.Status.Should().Be("Open");
        targetQuiz.ReattemptStatus.Should().Be("Approved");
    }

    [Fact]
    public async Task StartQuizAttemptAsync_ShouldReturnBilingualTextEnAndTextHi_WhenPresent()
    {
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "General Knowledge" };
        await context.Categories.AddAsync(category);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Bilingual GK Test",
            Description = "General Knowledge Assessment",
            CategoryId = category.Id,
            DurationMinutes = 15,
            StartAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            PassingPercent = 50,
            MaxAttempts = 1,
            IsPublished = true,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        var q = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is the capital of India?",
            TextHi = "भारत की राजधानी क्या है?",
            Type = QuestionType.SingleChoice,
            Marks = 2,
            Order = 1,
            IsDeleted = false
        };
        q.Options.Add(new Option
        {
            Id = Guid.NewGuid(),
            QuestionId = q.Id,
            Text = "New Delhi",
            TextHi = "नई दिल्ली",
            IsCorrect = true
        });
        q.Options.Add(new Option
        {
            Id = Guid.NewGuid(),
            QuestionId = q.Id,
            Text = "Mumbai",
            TextHi = "मुंबई",
            IsCorrect = false
        });

        await context.Questions.AddAsync(q);
        await context.SaveChangesAsync();

        var service = new QuizAttemptService(
            context,
            _currentUserMock.Object,
            _redisService,
            _backgroundJobClientMock.Object);

        var result = await service.StartQuizAttemptAsync(quiz.Id);

        result.Should().NotBeNull();
        result.Questions.Should().HaveCount(1);
        var resQ = result.Questions.First();
        resQ.Text.Should().Be("What is the capital of India?");
        resQ.TextEn.Should().Be("What is the capital of India?");
        resQ.TextHi.Should().Be("भारत की राजधानी क्या है?");

        resQ.Options.Should().HaveCount(2);
        var delhiOpt = resQ.Options.First(o => o.Text == "New Delhi");
        delhiOpt.TextEn.Should().Be("New Delhi");
        delhiOpt.TextHi.Should().Be("नई दिल्ली");

        // Resuming active attempt returns the exact same bilingual fields
        var activeAttempt = await service.GetActiveAttemptAsync(result.AttemptId);
        activeAttempt.Should().NotBeNull();
        var activeQ = activeAttempt!.Questions.First();
        activeQ.TextHi.Should().Be("भारत की राजधानी क्या है?");
        activeQ.Options.First(o => o.Text == "New Delhi").TextHi.Should().Be("नई दिल्ली");
    }
}
