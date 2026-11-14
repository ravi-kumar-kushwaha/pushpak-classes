using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Quizzes.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Quizzes;

public class QuizServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Guid _adminUserId = Guid.NewGuid();

    public QuizServiceTests()
    {
        _currentUserMock.Setup(u => u.UserId).Returns(_adminUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Admin");
    }

    private PushpakClassesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PushpakClassesDbContext(options);
    }

    [Fact]
    public async Task CreateQuizAsync_ValidQuiz_CreatesAndWritesAuditLog()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "General Science" };
        await context.Categories.AddAsync(category);
        await context.SaveChangesAsync();

        var service = new QuizService(context, _currentUserMock.Object);

        var request = new CreateQuizRequestDto
        {
            Title = "Science Quiz 1",
            Description = "Introductory Science",
            CategoryId = category.Id,
            DurationMinutes = 45,
            StartAtUtc = DateTime.UtcNow.AddHours(1),
            EndAtUtc = DateTime.UtcNow.AddHours(3),
            PassingPercent = 60,
            MaxAttempts = 2,
            ShuffleQuestions = true,
            ShuffleOptions = true,
            IsPublished = false
        };

        // Act
        var result = await service.CreateQuizAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Science Quiz 1");
        result.CategoryName.Should().Be("General Science");
        result.DurationMinutes.Should().Be(45);
        result.PassingPercent.Should().Be(60);
        result.IsPublished.Should().BeFalse();

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == result.Id.ToString() && a.Action == "AdminCreateQuiz");
        audit.Should().NotBeNull();
        audit!.UserId.Should().Be(_adminUserId);
    }

    [Fact]
    public async Task CreateQuizAsync_StartAtAfterEndAt_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "History" };
        await context.Categories.AddAsync(category);
        await context.SaveChangesAsync();

        var service = new QuizService(context, _currentUserMock.Object);

        var request = new CreateQuizRequestDto
        {
            Title = "Invalid Quiz",
            CategoryId = category.Id,
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow.AddHours(4),
            EndAtUtc = DateTime.UtcNow.AddHours(2) // Start > End
        };

        // Act & Assert
        var act = () => service.CreateQuizAsync(request);
        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("*earlier than end time*");
    }

    [Fact]
    public async Task CreateQuizAsync_DurationZeroOrNegative_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Math" };
        await context.Categories.AddAsync(category);
        await context.SaveChangesAsync();

        var service = new QuizService(context, _currentUserMock.Object);

        var request = new CreateQuizRequestDto
        {
            Title = "Zero Duration Quiz",
            CategoryId = category.Id,
            DurationMinutes = 0,
            StartAtUtc = DateTime.UtcNow.AddHours(1),
            EndAtUtc = DateTime.UtcNow.AddHours(2)
        };

        // Act & Assert
        var act = () => service.CreateQuizAsync(request);
        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("*greater than 0 minutes*");
    }

    [Fact]
    public async Task CreateQuizAsync_NonExistingCategory_ThrowsNotFound()
    {
        // Arrange
        using var context = CreateDbContext();
        var service = new QuizService(context, _currentUserMock.Object);

        var request = new CreateQuizRequestDto
        {
            Title = "No Category Quiz",
            CategoryId = Guid.NewGuid(),
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow.AddHours(1),
            EndAtUtc = DateTime.UtcNow.AddHours(2)
        };

        // Act & Assert
        var act = () => service.CreateQuizAsync(request);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetQuizzesAsync_PagedSearchAndFiltering_ReturnsCorrectResults()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat1 = new Category { Id = Guid.NewGuid(), Name = "Physics" };
        var cat2 = new Category { Id = Guid.NewGuid(), Name = "Chemistry" };
        await context.Categories.AddRangeAsync(cat1, cat2);

        for (int i = 1; i <= 10; i++)
        {
            await context.Quizzes.AddAsync(new Quiz
            {
                Id = Guid.NewGuid(),
                Title = i % 2 == 0 ? $"Physics Term {i}" : $"Chemistry Midterm {i}",
                CategoryId = i % 2 == 0 ? cat1.Id : cat2.Id,
                DurationMinutes = 20 + i,
                StartAtUtc = DateTime.UtcNow.AddDays(i),
                EndAtUtc = DateTime.UtcNow.AddDays(i + 1),
                IsPublished = i > 5,
                IsDeleted = false
            });
        }
        await context.SaveChangesAsync();

        var service = new QuizService(context, _currentUserMock.Object);

        // Act 1: Search by Physics
        var physicsResult = await service.GetQuizzesAsync(new QuizQueryParameters
        {
            SearchQuery = "Physics",
            PageNumber = 1,
            PageSize = 10
        });

        // Assert 1
        physicsResult.Items.Should().HaveCount(5);
        physicsResult.Items.Should().OnlyContain(q => q.Title.Contains("Physics"));

        // Act 2: Filter by Published
        var publishedResult = await service.GetQuizzesAsync(new QuizQueryParameters
        {
            IsPublished = true,
            PageNumber = 1,
            PageSize = 10
        });

        // Assert 2
        publishedResult.Items.Should().HaveCount(5);
        publishedResult.Items.Should().OnlyContain(q => q.IsPublished);
    }

    [Fact]
    public async Task UpdateQuizAsync_UpdatesPropertiesAndWritesAuditLog()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Biology" };
        await context.Categories.AddAsync(cat);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Original Quiz",
            CategoryId = cat.Id,
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow.AddHours(1),
            EndAtUtc = DateTime.UtcNow.AddHours(2),
            PassingPercent = 50,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);
        await context.SaveChangesAsync();

        var service = new QuizService(context, _currentUserMock.Object);

        // Act
        var updateRequest = new UpdateQuizRequestDto
        {
            Title = "Updated Quiz Title",
            Description = "Updated Desc",
            CategoryId = cat.Id,
            DurationMinutes = 60,
            StartAtUtc = DateTime.UtcNow.AddHours(1),
            EndAtUtc = DateTime.UtcNow.AddHours(3),
            PassingPercent = 75,
            MaxAttempts = 1,
            ShuffleQuestions = true,
            ShuffleOptions = false
        };
        var result = await service.UpdateQuizAsync(quiz.Id, updateRequest);

        // Assert
        result.Title.Should().Be("Updated Quiz Title");
        result.DurationMinutes.Should().Be(60);
        result.PassingPercent.Should().Be(75);

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == quiz.Id.ToString() && a.Action == "AdminUpdateQuiz");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteQuizAsync_SoftDeletesQuiz()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Math" };
        await context.Categories.AddAsync(cat);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Quiz to Delete",
            CategoryId = cat.Id,
            DurationMinutes = 20,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);
        await context.SaveChangesAsync();

        var service = new QuizService(context, _currentUserMock.Object);

        // Act
        var result = await service.DeleteQuizAsync(quiz.Id);

        // Assert
        result.Message.Should().Contain("deleted successfully");

        // Should be marked soft-deleted
        var dbQuiz = await context.Quizzes.IgnoreQueryFilters().FirstOrDefaultAsync(q => q.Id == quiz.Id);
        dbQuiz.Should().NotBeNull();
        dbQuiz!.IsDeleted.Should().BeTrue();

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == quiz.Id.ToString() && a.Action == "AdminDeleteQuiz");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task SetPublishStatusAsync_PublishWithoutQuestions_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Math" };
        await context.Categories.AddAsync(cat);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Empty Quiz",
            CategoryId = cat.Id,
            DurationMinutes = 20,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            IsPublished = false,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);
        await context.SaveChangesAsync();

        var service = new QuizService(context, _currentUserMock.Object);

        // Act & Assert: Cannot publish with 0 questions
        var act = () => service.SetPublishStatusAsync(quiz.Id, true);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("0 questions");
    }

    [Fact]
    public async Task SetPublishStatusAsync_PublishWithInvalidSchedule_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Math" };
        await context.Categories.AddAsync(cat);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Invalid Schedule Quiz",
            CategoryId = cat.Id,
            DurationMinutes = 20,
            StartAtUtc = DateTime.UtcNow.AddHours(2),
            EndAtUtc = DateTime.UtcNow.AddHours(1), // End before Start!
            IsPublished = false,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        await context.Questions.AddAsync(new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is 1 + 1?",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Order = 1,
            IsDeleted = false
        });
        await context.SaveChangesAsync();

        var service = new QuizService(context, _currentUserMock.Object);

        // Act & Assert
        var act = () => service.SetPublishStatusAsync(quiz.Id, true);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("StartAtUtc is not earlier than EndAtUtc");
    }

    [Fact]
    public async Task SetPublishStatusAsync_PublishWithQuestions_PublishesQuiz()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Math" };
        await context.Categories.AddAsync(cat);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Valid Quiz with Question",
            CategoryId = cat.Id,
            DurationMinutes = 20,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            IsPublished = false,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        await context.Questions.AddAsync(new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is 2 + 2?",
            Type = QuestionType.SingleChoice,
            Marks = 2,
            Order = 1,
            IsDeleted = false
        });
        await context.SaveChangesAsync();

        var service = new QuizService(context, _currentUserMock.Object);

        // Act
        var result = await service.SetPublishStatusAsync(quiz.Id, true);

        // Assert
        result.IsPublished.Should().BeTrue();

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == quiz.Id.ToString() && a.Action == "AdminPublishQuiz");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task SetPublishStatusAsync_WhenPublished_EnqueuesBroadcastEmailToActiveStudents()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Robotics" };
        await context.Categories.AddAsync(cat);

        var activeStudent = new User { Id = Guid.NewGuid(), FullName = "Active Learner", Email = "student@robotics.com", Role = UserRole.User, IsActive = true, PasswordHash = "h" };
        var inactiveStudent = new User { Id = Guid.NewGuid(), FullName = "Inactive Learner", Email = "inactive@robotics.com", Role = UserRole.User, IsActive = false, PasswordHash = "h" };
        await context.Users.AddRangeAsync(activeStudent, inactiveStudent);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Intro to Robotics",
            CategoryId = cat.Id,
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(2),
            PassingPercent = 60,
            IsPublished = false,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);

        await context.Questions.AddAsync(new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is a sensor?",
            Type = QuestionType.SingleChoice,
            Marks = 5,
            Order = 1,
            IsDeleted = false
        });
        await context.SaveChangesAsync();

        var bgJobClientMock = new Mock<Hangfire.IBackgroundJobClient>();
        var emailServiceMock = new Mock<IEmailService>();
        var service = new QuizService(context, _currentUserMock.Object, emailServiceMock.Object, bgJobClientMock.Object);

        // Act
        await service.SetPublishStatusAsync(quiz.Id, true);

        // Assert: Job enqueued for active student only
        bgJobClientMock.Verify(x => x.Create(
            It.IsAny<Hangfire.Common.Job>(),
            It.IsAny<Hangfire.States.IState>()), Times.Once);
    }

    [Fact]
    public async Task DuplicateQuizAsync_DeepClonesQuizQuestionsAndOptionsWithNewGuidsAndUnpublished()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Science" };
        await context.Categories.AddAsync(cat);

        var originalQuiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Original Science Quiz",
            Description = "Original Description",
            CategoryId = cat.Id,
            DurationMinutes = 40,
            StartAtUtc = DateTime.UtcNow.AddHours(2),
            EndAtUtc = DateTime.UtcNow.AddHours(4),
            PassingPercent = 70,
            IsPublished = true,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(originalQuiz);

        var originalQuestion = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = originalQuiz.Id,
            Text = "What is H2O?",
            Type = QuestionType.SingleChoice,
            Marks = 5,
            Order = 1,
            IsDeleted = false
        };
        originalQuestion.Options.Add(new Option { Id = Guid.NewGuid(), QuestionId = originalQuestion.Id, Text = "Water", IsCorrect = true });
        originalQuestion.Options.Add(new Option { Id = Guid.NewGuid(), QuestionId = originalQuestion.Id, Text = "Gold", IsCorrect = false });
        await context.Questions.AddAsync(originalQuestion);
        await context.SaveChangesAsync();

        var service = new QuizService(context, _currentUserMock.Object);

        // Act
        var duplicated = await service.DuplicateQuizAsync(originalQuiz.Id);

        // Assert
        duplicated.Should().NotBeNull();
        duplicated.Id.Should().NotBe(originalQuiz.Id);
        duplicated.Title.Should().Be("Original Science Quiz (Copy)");
        duplicated.IsPublished.Should().BeFalse(); // Always unpublished on duplication

        var dbDuplicatedQuestions = await context.Questions
            .Include(q => q.Options)
            .Where(q => q.QuizId == duplicated.Id)
            .ToListAsync();

        dbDuplicatedQuestions.Should().HaveCount(1);
        var clonedQuestion = dbDuplicatedQuestions[0];
        clonedQuestion.Id.Should().NotBe(originalQuestion.Id);
        clonedQuestion.Text.Should().Be("What is H2O?");
        clonedQuestion.Options.Should().HaveCount(2);
        clonedQuestion.Options.Should().OnlyContain(o => o.Id != Guid.Empty);
        clonedQuestion.Options.Select(o => o.Id).Should().NotContain(originalQuestion.Options.Select(o => o.Id));

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == duplicated.Id.ToString() && a.Action == "AdminDuplicateQuiz");
        audit.Should().NotBeNull();
    }
}
