using System.Net;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Reports.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Reports;

public class ReportServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Guid _userId = Guid.NewGuid();

    public ReportServiceTests()
    {
        _currentUserMock.Setup(u => u.UserId).Returns(_userId);
        _currentUserMock.Setup(u => u.Role).Returns("User");
    }

    private PushpakClassesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PushpakClassesDbContext(options);
    }

    [Fact]
    public async Task GetUserAttemptsAsync_ReturnsPagedHistoryForCurrentUser()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Computer Science" };
        await context.Categories.AddAsync(category);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Data Structures Exam",
            CategoryId = category.Id,
            DurationMinutes = 45,
            StartAtUtc = DateTime.UtcNow.AddDays(-2),
            EndAtUtc = DateTime.UtcNow.AddDays(2),
            IsPublished = true,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        for (int i = 1; i <= 3; i++)
        {
            await context.QuizAttempts.AddAsync(new QuizAttempt
            {
                Id = Guid.NewGuid(),
                QuizId = quiz.Id,
                UserId = _userId,
                Status = QuizAttemptStatus.Submitted,
                StartedAtUtc = DateTime.UtcNow.AddHours(-i),
                SubmittedAtUtc = DateTime.UtcNow.AddHours(-i).AddMinutes(20),
                Score = 80 + i,
                TotalMarks = 100,
                Percent = 80 + i,
                Passed = true
            });
        }
        await context.SaveChangesAsync();

        var service = new ReportService(context, _currentUserMock.Object);

        // Act
        var result = await service.GetUserAttemptsAsync(new QuizQueryParameters { PageNumber = 1, PageSize = 10 });

        // Assert
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.Items[0].QuizTitle.Should().Be("Data Structures Exam");
        result.Items[0].CategoryName.Should().Be("Computer Science");
        result.Items[0].Passed.Should().BeTrue();
    }

    [Fact]
    public async Task GetAttemptReviewAsync_WhenAttemptInProgress_ThrowsBadRequest()
    {
        // Arrange (Security Guard: Review forbidden before submission)
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Physics" };
        await context.Categories.AddAsync(category);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Quantum Physics",
            CategoryId = category.Id,
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            IsPublished = true,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        var inProgressAttempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.InProgress, // Active!
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(25)
        };
        await context.QuizAttempts.AddAsync(inProgressAttempt);
        await context.SaveChangesAsync();

        var service = new ReportService(context, _currentUserMock.Object);

        // Act & Assert
        var act = () => service.GetAttemptReviewAsync(inProgressAttempt.Id);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("only accessible after submission");
    }

    [Fact]
    public async Task GetAttemptReviewAsync_WhenAttemptCompleted_ReturnsQuestionsWithSelectedAndCorrectOptions()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Biology" };
        await context.Categories.AddAsync(category);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Cell Biology",
            CategoryId = category.Id,
            DurationMinutes = 20,
            StartAtUtc = DateTime.UtcNow.AddHours(-1),
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            IsPublished = true,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        var q1 = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is the powerhouse of the cell?",
            Type = QuestionType.SingleChoice,
            Marks = 5,
            Order = 1,
            IsDeleted = false
        };
        var optCorrect = new Option { Id = Guid.NewGuid(), QuestionId = q1.Id, Text = "Mitochondria", IsCorrect = true };
        var optWrong = new Option { Id = Guid.NewGuid(), QuestionId = q1.Id, Text = "Nucleus", IsCorrect = false };
        q1.Options.Add(optCorrect);
        q1.Options.Add(optWrong);
        await context.Questions.AddAsync(q1);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            EndsAtUtc = DateTime.UtcNow,
            SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            Score = 5,
            TotalMarks = 5,
            Percent = 100,
            Passed = true
        };
        await context.QuizAttempts.AddAsync(attempt);

        // Student selected correct option
        await context.AttemptAnswers.AddAsync(new AttemptAnswer
        {
            Id = Guid.NewGuid(),
            AttemptId = attempt.Id,
            QuestionId = q1.Id,
            SelectedOptionIds = optCorrect.Id.ToString(),
            IsCorrect = true,
            MarksAwarded = 5
        });

        await context.SaveChangesAsync();

        var service = new ReportService(context, _currentUserMock.Object);

        // Act
        var review = await service.GetAttemptReviewAsync(attempt.Id);

        // Assert
        review.Should().NotBeNull();
        review.AttemptId.Should().Be(attempt.Id);
        review.QuizTitle.Should().Be("Cell Biology");
        review.Score.Should().Be(5);
        review.Passed.Should().BeTrue();
        review.Questions.Should().HaveCount(1);

        var qReview = review.Questions[0];
        qReview.QuestionId.Should().Be(q1.Id);
        qReview.IsCorrect.Should().BeTrue();
        qReview.MarksAwarded.Should().Be(5);
        qReview.Options.Should().HaveCount(2);

        var optReviewCorrect = qReview.Options.Single(o => o.OptionId == optCorrect.Id);
        optReviewCorrect.IsCorrect.Should().BeTrue();
        optReviewCorrect.IsSelectedByStudent.Should().BeTrue();

        var optReviewWrong = qReview.Options.Single(o => o.OptionId == optWrong.Id);
        optReviewWrong.IsCorrect.Should().BeFalse();
        optReviewWrong.IsSelectedByStudent.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserDashboardAsync_CalculatesCorrectMetrics()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "History" };
        await context.Categories.AddAsync(category);

        var quiz1 = new Quiz { Id = Guid.NewGuid(), Title = "World War II", CategoryId = category.Id, DurationMinutes = 30, StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(1), IsPublished = true, IsDeleted = false };
        var quiz2 = new Quiz { Id = Guid.NewGuid(), Title = "Ancient Rome", CategoryId = category.Id, DurationMinutes = 30, StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(1), IsPublished = true, IsDeleted = false };
        await context.Quizzes.AddRangeAsync(quiz1, quiz2);

        // 3 completed attempts:
        // Attempt 1: 80% (Passed)
        // Attempt 2: 100% (Passed)
        // Attempt 3: 40% (Failed)
        // Total = 3, Avg = (80+100+40)/3 = 73.33%, Best = 100%, PassRate = 2/3 = 66.67%
        await context.QuizAttempts.AddRangeAsync(
            new QuizAttempt { Id = Guid.NewGuid(), QuizId = quiz1.Id, UserId = _userId, Status = QuizAttemptStatus.Submitted, Percent = 80, Score = 8, TotalMarks = 10, Passed = true, StartedAtUtc = DateTime.UtcNow.AddHours(-3) },
            new QuizAttempt { Id = Guid.NewGuid(), QuizId = quiz1.Id, UserId = _userId, Status = QuizAttemptStatus.Submitted, Percent = 100, Score = 10, TotalMarks = 10, Passed = true, StartedAtUtc = DateTime.UtcNow.AddHours(-2) },
            new QuizAttempt { Id = Guid.NewGuid(), QuizId = quiz2.Id, UserId = _userId, Status = QuizAttemptStatus.AutoSubmitted, Percent = 40, Score = 4, TotalMarks = 10, Passed = false, StartedAtUtc = DateTime.UtcNow.AddHours(-1) }
        );
        await context.SaveChangesAsync();

        var service = new ReportService(context, _currentUserMock.Object);

        // Act
        var dashboard = await service.GetUserDashboardAsync();

        // Assert
        dashboard.TotalAttempts.Should().Be(3);
        dashboard.AverageScore.Should().Be(73.33m);
        dashboard.BestScore.Should().Be(100m);
        dashboard.PassRate.Should().Be(66.67m);
        dashboard.TotalQuizzesAttempted.Should().Be(2);
        dashboard.RecentAttempts.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAdminDashboardAsync_CalculatesKPIsAndThirtyDayTrend()
    {
        // Arrange
        using var context = CreateDbContext();

        await context.Users.AddRangeAsync(
            new User { Id = Guid.NewGuid(), FullName = "U1", Email = "u1@test.com", Role = UserRole.User, IsActive = true, PasswordHash = "h" },
            new User { Id = Guid.NewGuid(), FullName = "U2", Email = "u2@test.com", Role = UserRole.User, IsActive = true, PasswordHash = "h" }
        );

        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "Admin Test Quiz", CategoryId = Guid.NewGuid(), DurationMinutes = 30, StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(1), IsPublished = true, IsDeleted = false };
        await context.Quizzes.AddAsync(quiz);

        await context.QuizAttempts.AddAsync(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = Guid.NewGuid(),
            Status = QuizAttemptStatus.Submitted,
            Percent = 90,
            Score = 9,
            TotalMarks = 10,
            Passed = true,
            StartedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var service = new ReportService(context, _currentUserMock.Object);

        // Act
        var dashboard = await service.GetAdminDashboardAsync();

        // Assert
        dashboard.TotalUsers.Should().Be(2);
        dashboard.TotalQuizzes.Should().Be(1);
        dashboard.TotalAttempts.Should().Be(1);
        dashboard.AverageScore.Should().Be(90m);
        dashboard.TrendLast30Days.Should().HaveCount(30);
    }

    [Fact]
    public async Task GetQuizLeaderboardAsync_RanksByScoreDescendingAndTieBreaksByCompletionTime()
    {
        // Arrange
        using var context = CreateDbContext();

        var studentFast = new User { Id = Guid.NewGuid(), FullName = "Fast Student", Email = "fast@test.com", Role = UserRole.User, PasswordHash = "h" };
        var studentSlow = new User { Id = Guid.NewGuid(), FullName = "Slow Student", Email = "slow@test.com", Role = UserRole.User, PasswordHash = "h" };
        var studentLowScore = new User { Id = Guid.NewGuid(), FullName = "Low Student", Email = "low@test.com", Role = UserRole.User, PasswordHash = "h" };
        await context.Users.AddRangeAsync(studentFast, studentSlow, studentLowScore);

        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "Speed Math", CategoryId = Guid.NewGuid(), DurationMinutes = 30, StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(1), IsPublished = true, IsDeleted = false };
        await context.Quizzes.AddAsync(quiz);

        // Both Fast and Slow scored 100%, but Fast took 10 minutes, Slow took 25 minutes
        var now = DateTime.UtcNow;
        await context.QuizAttempts.AddRangeAsync(
            new QuizAttempt { Id = Guid.NewGuid(), QuizId = quiz.Id, UserId = studentSlow.Id, Status = QuizAttemptStatus.Submitted, Score = 100, TotalMarks = 100, Percent = 100, Passed = true, StartedAtUtc = now.AddMinutes(-30), SubmittedAtUtc = now.AddMinutes(-5) }, // 25 min
            new QuizAttempt { Id = Guid.NewGuid(), QuizId = quiz.Id, UserId = studentFast.Id, Status = QuizAttemptStatus.Submitted, Score = 100, TotalMarks = 100, Percent = 100, Passed = true, StartedAtUtc = now.AddMinutes(-30), SubmittedAtUtc = now.AddMinutes(-20) }, // 10 min
            new QuizAttempt { Id = Guid.NewGuid(), QuizId = quiz.Id, UserId = studentLowScore.Id, Status = QuizAttemptStatus.Submitted, Score = 50, TotalMarks = 100, Percent = 50, Passed = false, StartedAtUtc = now.AddMinutes(-30), SubmittedAtUtc = now.AddMinutes(-25) }
        );
        await context.SaveChangesAsync();

        var service = new ReportService(context, _currentUserMock.Object);

        // Act
        var result = await service.GetQuizLeaderboardAsync(quiz.Id);

        // Assert
        result.TotalParticipants.Should().Be(3);
        result.Leaderboard.Should().HaveCount(3);

        // Rank 1: Fast Student (Score 100, 10 min)
        result.Leaderboard[0].Rank.Should().Be(1);
        result.Leaderboard[0].FullName.Should().Be("Fast Student");
        result.Leaderboard[0].DurationMinutesTaken.Should().Be(10);

        // Rank 2: Slow Student (Score 100, 25 min)
        result.Leaderboard[1].Rank.Should().Be(2);
        result.Leaderboard[1].FullName.Should().Be("Slow Student");
        result.Leaderboard[1].DurationMinutesTaken.Should().Be(25);

        // Rank 3: Low Student (Score 50)
        result.Leaderboard[2].Rank.Should().Be(3);
        result.Leaderboard[2].FullName.Should().Be("Low Student");
    }

    [Fact]
    public async Task ExportQuizResultsToExcelAsync_GeneratesValidClosedXmlWorkbookWithHeadersAndRows()
    {
        // Arrange
        using var context = CreateDbContext();

        var student = new User { Id = Guid.NewGuid(), FullName = "Alice Learner", Email = "alice@pushpakclasses.com", Role = UserRole.User, PasswordHash = "h" };
        await context.Users.AddAsync(student);

        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "Chemistry Finals", CategoryId = Guid.NewGuid(), DurationMinutes = 45, StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(2), IsPublished = true, IsDeleted = false };
        await context.Quizzes.AddAsync(quiz);

        await context.QuizAttempts.AddAsync(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = student.Id,
            Status = QuizAttemptStatus.Submitted,
            Score = 95,
            TotalMarks = 100,
            Percent = 95,
            Passed = true,
            TabSwitchCount = 1,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-40),
            SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        });
        await context.SaveChangesAsync();

        var service = new ReportService(context, _currentUserMock.Object);

        // Act
        var (fileBytes, fileName) = await service.ExportQuizResultsToExcelAsync(quiz.Id);

        // Assert
        fileBytes.Should().NotBeNull();
        fileBytes.Length.Should().BeGreaterThan(0);
        fileName.Should().Contain("Chemistry_Finals_Results_").And.EndWith(".xlsx");

        // Verify valid ClosedXML workbook format
        using var memoryStream = new MemoryStream(fileBytes);
        using var workbook = new XLWorkbook(memoryStream);
        var worksheet = workbook.Worksheet("Results");
        worksheet.Should().NotBeNull();

        // Check header and content
        worksheet.Cell(1, 1).GetString().Should().Contain("PushpakClasses Quiz Results: Chemistry Finals");
        worksheet.Cell(3, 1).GetString().Should().Be("Rank");
        worksheet.Cell(3, 2).GetString().Should().Be("Student Name");
        worksheet.Cell(4, 2).GetString().Should().Be("Alice Learner");
        worksheet.Cell(4, 3).GetString().Should().Be("alice@pushpakclasses.com");
        worksheet.Cell(4, 4).GetValue<decimal>().Should().Be(95);
        worksheet.Cell(4, 7).GetString().Should().Be("PASSED");
    }

    [Fact]
    public async Task GetAttemptReviewAsync_OptionReviewIncludesBothIsSelectedAndIsSelectedByStudent()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Logic" };
        await context.Categories.AddAsync(category);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Logic Test",
            CategoryId = category.Id,
            DurationMinutes = 20,
            StartAtUtc = DateTime.UtcNow.AddHours(-1),
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            IsPublished = true,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        var q = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "Is 2 prime?",
            Type = QuestionType.TrueFalse,
            Marks = 5,
            Order = 1,
            IsDeleted = false
        };
        var opt1 = new Option { Id = Guid.NewGuid(), QuestionId = q.Id, Text = "True", IsCorrect = true };
        var opt2 = new Option { Id = Guid.NewGuid(), QuestionId = q.Id, Text = "False", IsCorrect = false };
        q.Options.Add(opt1);
        q.Options.Add(opt2);
        await context.Questions.AddAsync(q);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            Score = 5,
            TotalMarks = 5,
            Percent = 100,
            Passed = true
        };
        var answer = new AttemptAnswer
        {
            Id = Guid.NewGuid(),
            AttemptId = attempt.Id,
            QuestionId = q.Id,
            SelectedOptionIds = opt1.Id.ToString(),
            IsCorrect = true,
            MarksAwarded = 5
        };
        attempt.AttemptAnswers.Add(answer);
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var service = new ReportService(context, _currentUserMock.Object);

        // Act
        var review = await service.GetAttemptReviewAsync(attempt.Id);

        // Assert
        review.Should().NotBeNull();
        review.Questions.Should().HaveCount(1);
        var qReview = review.Questions[0];
        qReview.Options.Should().HaveCount(2);

        var trueOpt = qReview.Options.First(o => o.OptionId == opt1.Id);
        trueOpt.IsSelected.Should().BeTrue();
        trueOpt.IsSelectedByStudent.Should().BeTrue();
        trueOpt.IsCorrect.Should().BeTrue();

        var falseOpt = qReview.Options.First(o => o.OptionId == opt2.Id);
        falseOpt.IsSelected.Should().BeFalse();
        falseOpt.IsSelectedByStudent.Should().BeFalse();
        falseOpt.IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserAttemptsAsync_FilterByCategoryAndSearchAndSortsNewestFirst()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat1 = new Category { Id = Guid.NewGuid(), Name = "Category 1" };
        var cat2 = new Category { Id = Guid.NewGuid(), Name = "Category 2" };
        await context.Categories.AddRangeAsync(cat1, cat2);

        var quiz1 = new Quiz { Id = Guid.NewGuid(), Title = "Alpha Quiz", CategoryId = cat1.Id, IsPublished = true };
        var quiz2 = new Quiz { Id = Guid.NewGuid(), Title = "Beta Quiz", CategoryId = cat2.Id, IsPublished = true };
        await context.Quizzes.AddRangeAsync(quiz1, quiz2);

        var now = DateTime.UtcNow;
        var attemptOld = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz1.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = now.AddDays(-2),
            SubmittedAtUtc = now.AddDays(-2).AddMinutes(15)
        };
        var attemptNew = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz1.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = now.AddHours(-1),
            SubmittedAtUtc = now.AddHours(-1).AddMinutes(15)
        };
        var attemptCat2 = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz2.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = now.AddMinutes(-10),
            SubmittedAtUtc = now.AddMinutes(-5)
        };
        await context.QuizAttempts.AddRangeAsync(attemptOld, attemptNew, attemptCat2);
        await context.SaveChangesAsync();

        var service = new ReportService(context, _currentUserMock.Object);

        // Act - Default sort (newest first)
        var defaultList = await service.GetUserAttemptsAsync(new QuizQueryParameters { PageNumber = 1, PageSize = 10 });
        defaultList.Items[0].Id.Should().Be(attemptCat2.Id); // most recent
        defaultList.Items[1].Id.Should().Be(attemptNew.Id);
        defaultList.Items[2].Id.Should().Be(attemptOld.Id);

        // Act - Filter by category
        var cat1List = await service.GetUserAttemptsAsync(new QuizQueryParameters { CategoryId = cat1.Id, PageNumber = 1, PageSize = 10 });
        cat1List.TotalCount.Should().Be(2);

        // Act - Search
        var searchList = await service.GetUserAttemptsAsync(new QuizQueryParameters { SearchQuery = "Beta", PageNumber = 1, PageSize = 10 });
        searchList.TotalCount.Should().Be(1);
        searchList.Items[0].QuizTitle.Should().Be("Beta Quiz");
    }

    [Fact]
    public async Task GetAttemptReviewAsync_UnscoredSubmittedAttempt_TriggersScoringOnDemandAndReturnsScore()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "General Science" };
        await context.Categories.AddAsync(category);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Scoring Test Quiz",
            CategoryId = category.Id,
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow.AddHours(-1),
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            PassingPercent = 40,
            IsPublished = true
        };
        await context.Quizzes.AddAsync(quiz);

        var q1 = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is 2 + 2?",
            Type = QuestionType.SingleChoice,
            Marks = 5,
            Order = 1,
            IsDeleted = false
        };
        var optCorrect = new Option { Id = Guid.NewGuid(), QuestionId = q1.Id, Text = "4", IsCorrect = true };
        var optWrong = new Option { Id = Guid.NewGuid(), QuestionId = q1.Id, Text = "5", IsCorrect = false };
        q1.Options.Add(optCorrect);
        q1.Options.Add(optWrong);
        await context.Questions.AddAsync(q1);

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = _userId,
            Status = QuizAttemptStatus.Submitted,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            SubmittedAtUtc = DateTime.UtcNow,
            Score = 0,
            Percent = 0,
            Passed = false
        };
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var scoringServiceMock = new Mock<IScoringService>();
        scoringServiceMock.Setup(s => s.ScoreAttemptAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, CancellationToken ct) =>
            {
                var att = await context.QuizAttempts.Include(a => a.AttemptAnswers).FirstOrDefaultAsync(a => a.Id == id, ct);
                if (att != null)
                {
                    att.Score = 5;
                    att.Percent = 100;
                    att.Passed = true;
                    var ans = new AttemptAnswer
                    {
                        Id = Guid.NewGuid(),
                        AttemptId = att.Id,
                        QuestionId = q1.Id,
                        SelectedOptionIds = optCorrect.Id.ToString(),
                        IsCorrect = true,
                        MarksAwarded = 5
                    };
                    await context.AttemptAnswers.AddAsync(ans, ct);
                    await context.SaveChangesAsync(ct);
                }
            });

        var service = new ReportService(context, _currentUserMock.Object, scoringServiceMock.Object);

        // Act
        var review = await service.GetAttemptReviewAsync(attempt.Id);

        // Assert
        scoringServiceMock.Verify(s => s.ScoreAttemptAsync(attempt.Id, It.IsAny<CancellationToken>()), Times.Once);
        review.Should().NotBeNull();
        review.Score.Should().Be(5);
        review.Passed.Should().BeTrue();
        review.IsScoringPending.Should().BeFalse();
    }
}
