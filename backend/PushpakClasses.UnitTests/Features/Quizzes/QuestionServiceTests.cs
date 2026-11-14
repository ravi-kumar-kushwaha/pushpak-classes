using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Features.Quizzes.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Quizzes;

public class QuestionServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly IQuestionImportService _importService = new QuestionImportService();
    private readonly Guid _adminUserId = Guid.NewGuid();

    public QuestionServiceTests()
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

    private async Task<Quiz> CreateTestQuizAsync(PushpakClassesDbContext context, bool isPublished = false)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Sample Category" };
        await context.Categories.AddAsync(category);

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Sample Quiz",
            CategoryId = category.Id,
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(2),
            IsPublished = isPublished,
            IsDeleted = false
        };
        await context.Quizzes.AddAsync(quiz);
        await context.SaveChangesAsync();

        return quiz;
    }

    [Fact]
    public async Task CreateQuestionAsync_SingleChoice_WithExactlyOneCorrect_Succeeds()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context);
        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        var request = new CreateQuestionRequestDto
        {
            Text = "What is 2 + 2?",
            Type = QuestionType.SingleChoice,
            Marks = 2,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "3", IsCorrect = false },
                new() { Text = "4", IsCorrect = true },
                new() { Text = "5", IsCorrect = false }
            }
        };

        // Act
        var result = await service.CreateQuestionAsync(quiz.Id, request);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().Be("What is 2 + 2?");
        result.Type.Should().Be("SingleChoice");
        result.Marks.Should().Be(2);
        result.Options.Should().HaveCount(3);
        result.Options.Count(o => o.IsCorrect).Should().Be(1);

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == result.Id.ToString() && a.Action == "AdminCreateQuestion");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateQuestionAsync_SingleChoice_WithMultipleOrZeroCorrect_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context);
        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        // Act & Assert 1: Zero correct
        var zeroCorrect = new CreateQuestionRequestDto
        {
            Text = "Zero Correct",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "A", IsCorrect = false },
                new() { Text = "B", IsCorrect = false }
            }
        };
        var act1 = () => service.CreateQuestionAsync(quiz.Id, zeroCorrect);
        await act1.Should().ThrowAsync<AuthException>()
            .WithMessage("*must have exactly 1 correct option*");

        // Act & Assert 2: Multiple correct
        var multipleCorrect = new CreateQuestionRequestDto
        {
            Text = "Multiple Correct",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "A", IsCorrect = true },
                new() { Text = "B", IsCorrect = true }
            }
        };
        var act2 = () => service.CreateQuestionAsync(quiz.Id, multipleCorrect);
        await act2.Should().ThrowAsync<AuthException>()
            .WithMessage("*must have exactly 1 correct option*");
    }

    [Fact]
    public async Task CreateQuestionAsync_MultiChoice_WithMultipleCorrect_Succeeds()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context);
        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        var request = new CreateQuestionRequestDto
        {
            Text = "Which are programming languages?",
            Type = QuestionType.MultiChoice,
            Marks = 3,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "C#", IsCorrect = true },
                new() { Text = "HTML", IsCorrect = false },
                new() { Text = "Python", IsCorrect = true }
            }
        };

        // Act
        var result = await service.CreateQuestionAsync(quiz.Id, request);

        // Assert
        result.Options.Count(o => o.IsCorrect).Should().Be(2);
    }

    [Fact]
    public async Task CreateQuestionAsync_MultiChoice_WithZeroCorrect_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context);
        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        var request = new CreateQuestionRequestDto
        {
            Text = "Select all that apply",
            Type = QuestionType.MultiChoice,
            Marks = 2,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "Opt 1", IsCorrect = false },
                new() { Text = "Opt 2", IsCorrect = false }
            }
        };

        // Act & Assert
        var act = () => service.CreateQuestionAsync(quiz.Id, request);
        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("*at least 1 correct option*");
    }

    [Fact]
    public async Task CreateQuestionAsync_TrueFalse_WithTwoFixedOptions_Succeeds()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context);
        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        var request = new CreateQuestionRequestDto
        {
            Text = "The sun rises in the east.",
            Type = QuestionType.TrueFalse,
            Marks = 1,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "True", IsCorrect = true },
                new() { Text = "False", IsCorrect = false }
            }
        };

        // Act
        var result = await service.CreateQuestionAsync(quiz.Id, request);

        // Assert
        result.Options.Should().HaveCount(2);
        result.Options.Single(o => o.IsCorrect).Text.Should().Be("True");
    }

    [Fact]
    public async Task CreateQuestionAsync_TrueFalse_WithThreeOptions_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context);
        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        var request = new CreateQuestionRequestDto
        {
            Text = "The moon is made of cheese.",
            Type = QuestionType.TrueFalse,
            Marks = 1,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "True", IsCorrect = false },
                new() { Text = "False", IsCorrect = true },
                new() { Text = "Maybe", IsCorrect = false }
            }
        };

        // Act & Assert
        var act = () => service.CreateQuestionAsync(quiz.Id, request);
        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("*exactly 2 options*");
    }

    [Fact]
    public async Task CriticalGuard_PublishedQuizWithExistingAttempts_BlocksCreateQuestion()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context, isPublished: true);

        // Add a student attempt to the quiz
        await context.QuizAttempts.AddAsync(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(20),
            Status = QuizAttemptStatus.InProgress
        });
        await context.SaveChangesAsync();

        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        var request = new CreateQuestionRequestDto
        {
            Text = "New Question after Attempts",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "A", IsCorrect = true },
                new() { Text = "B", IsCorrect = false }
            }
        };

        // Act & Assert
        var act = () => service.CreateQuestionAsync(quiz.Id, request);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("Cannot modify questions for a published quiz that already has student attempts");
    }

    [Fact]
    public async Task CriticalGuard_PublishedQuizWithExistingAttempts_BlocksUpdateQuestion()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context, isPublished: true);

        var question = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "Pre-existing Question",
            Type = QuestionType.SingleChoice,
            Marks = 2,
            Order = 1,
            IsDeleted = false
        };
        question.Options.Add(new Option { Id = Guid.NewGuid(), QuestionId = question.Id, Text = "Opt 1", IsCorrect = true });
        question.Options.Add(new Option { Id = Guid.NewGuid(), QuestionId = question.Id, Text = "Opt 2", IsCorrect = false });
        await context.Questions.AddAsync(question);

        // Add attempt
        await context.QuizAttempts.AddAsync(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(25),
            Status = QuizAttemptStatus.InProgress
        });
        await context.SaveChangesAsync();

        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        var updateRequest = new UpdateQuestionRequestDto
        {
            Text = "Trying to change question",
            Type = QuestionType.SingleChoice,
            Marks = 5,
            Options = new List<CreateOptionDto>
            {
                new() { Text = "Opt 1", IsCorrect = true },
                new() { Text = "Opt 2", IsCorrect = false }
            }
        };

        // Act & Assert
        var act = () => service.UpdateQuestionAsync(quiz.Id, question.Id, updateRequest);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.Conflict);
        ex.Which.Message.Should().Contain("Quiz already has attempts, unpublish or duplicate it");
    }

    [Fact]
    public async Task PublishedQuizWithExistingAttempts_TypoFixOnly_Succeeds()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context, isPublished: true);

        var opt1Id = Guid.NewGuid();
        var opt2Id = Guid.NewGuid();

        var question = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "Original Typo Question",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Order = 1,
            IsDeleted = false,
            Options = new List<Option>
            {
                new() { Id = opt1Id, Text = "Opt 1 Original", IsCorrect = true },
                new() { Id = opt2Id, Text = "Opt 2 Original", IsCorrect = false }
            }
        };
        await context.Questions.AddAsync(question);

        await context.QuizAttempts.AddAsync(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(25),
            Status = QuizAttemptStatus.InProgress
        });
        await context.SaveChangesAsync();

        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        var typoUpdateRequest = new UpdateQuestionRequestDto
        {
            Text = "Fixed Typo Question Prompt",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Order = 1,
            Options = new List<CreateOptionDto>
            {
                new() { Id = opt1Id, Text = "Opt 1 Fixed Text", IsCorrect = true },
                new() { Id = opt2Id, Text = "Opt 2 Fixed Text", IsCorrect = false }
            }
        };

        // Act
        var result = await service.UpdateQuestionAsync(quiz.Id, question.Id, typoUpdateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().Be("Fixed Typo Question Prompt");
        result.Options.Should().HaveCount(2);
        result.Options.First(o => o.Id == opt1Id).Text.Should().Be("Opt 1 Fixed Text");
    }

    [Fact]
    public async Task UpdateQuestionAsync_WithoutAttempts_InPlaceOptionsTracking_Succeeds()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context, isPublished: false);

        var opt1Id = Guid.NewGuid();
        var opt2Id = Guid.NewGuid();

        var question = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "Question to Update In Place",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Order = 1,
            IsDeleted = false,
            Options = new List<Option>
            {
                new() { Id = opt1Id, Text = "Existing Opt 1", IsCorrect = true },
                new() { Id = opt2Id, Text = "Existing Opt 2 (Will be removed)", IsCorrect = false }
            }
        };
        await context.Questions.AddAsync(question);
        await context.SaveChangesAsync();

        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        var updateRequest = new UpdateQuestionRequestDto
        {
            Text = "Updated In Place Successfully",
            Type = QuestionType.SingleChoice,
            Marks = 2,
            Order = 1,
            Options = new List<CreateOptionDto>
            {
                new() { Id = opt1Id, Text = "Existing Opt 1 (Updated)", IsCorrect = true },
                new() { Id = null, Text = "Brand New Opt Added", IsCorrect = false }
            }
        };

        // Act
        var result = await service.UpdateQuestionAsync(quiz.Id, question.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().Be("Updated In Place Successfully");
        result.Marks.Should().Be(2);
        result.Options.Should().HaveCount(2);
        result.Options.Should().Contain(o => o.Id == opt1Id && o.Text == "Existing Opt 1 (Updated)");
        result.Options.Should().Contain(o => o.Id != opt1Id && o.Text == "Brand New Opt Added");
        result.Options.Should().NotContain(o => o.Id == opt2Id);
    }

    [Fact]
    public async Task CriticalGuard_PublishedQuizWithExistingAttempts_BlocksDeleteQuestion()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context, isPublished: true);

        var question = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "Question to delete",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Order = 1,
            IsDeleted = false
        };
        await context.Questions.AddAsync(question);

        await context.QuizAttempts.AddAsync(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(10),
            Status = QuizAttemptStatus.Submitted
        });
        await context.SaveChangesAsync();

        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        // Act & Assert
        var act = () => service.DeleteQuestionAsync(quiz.Id, question.Id);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("Cannot modify questions for a published quiz that already has student attempts");
    }

    [Fact]
    public async Task CriticalGuard_PublishedQuizWithExistingAttempts_BlocksReorderAndBulkImport()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context, isPublished: true);

        await context.QuizAttempts.AddAsync(new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EndsAtUtc = DateTime.UtcNow.AddMinutes(25),
            Status = QuizAttemptStatus.InProgress
        });
        await context.SaveChangesAsync();

        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        // Act & Assert 1: Reorder blocked
        var reorderAct = () => service.ReorderQuestionsAsync(quiz.Id, new ReorderQuestionsRequestDto
        {
            Orders = new List<QuestionOrderItemDto>
            {
                new() { QuestionId = Guid.NewGuid(), Order = 1 }
            }
        });
        await reorderAct.Should().ThrowAsync<AuthException>()
            .WithMessage("*Cannot modify questions for a published quiz that already has student attempts*");

        // Act & Assert 2: Bulk Import blocked
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("Text,Type,Marks,Option1,Option2,CorrectOptions\nSample,SingleChoice,1,A,B,1"));
        var importAct = () => service.BulkImportQuestionsAsync(quiz.Id, stream, "questions.csv");
        await importAct.Should().ThrowAsync<AuthException>()
            .WithMessage("*Cannot modify questions for a published quiz that already has student attempts*");
    }

    [Fact]
    public async Task ReorderQuestionsAsync_ValidOrdering_UpdatesSequenceAndOrderValues()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context, isPublished: false);

        var q1 = new Question { Id = Guid.NewGuid(), QuizId = quiz.Id, Text = "Q1", Order = 1, IsDeleted = false };
        var q2 = new Question { Id = Guid.NewGuid(), QuizId = quiz.Id, Text = "Q2", Order = 2, IsDeleted = false };
        var q3 = new Question { Id = Guid.NewGuid(), QuizId = quiz.Id, Text = "Q3", Order = 3, IsDeleted = false };
        await context.Questions.AddRangeAsync(q1, q2, q3);
        await context.SaveChangesAsync();

        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        // Act: Reorder to Q3 (Order 1), Q1 (Order 2), Q2 (Order 3)
        var reorderDto = new ReorderQuestionsRequestDto
        {
            Orders = new List<QuestionOrderItemDto>
            {
                new() { QuestionId = q3.Id, Order = 1 },
                new() { QuestionId = q1.Id, Order = 2 },
                new() { QuestionId = q2.Id, Order = 3 }
            }
        };
        var result = await service.ReorderQuestionsAsync(quiz.Id, reorderDto);

        // Assert
        result.Message.Should().Contain("reordered successfully");

        var dbQuestions = await context.Questions
            .Where(q => q.QuizId == quiz.Id)
            .OrderBy(q => q.Order)
            .ToListAsync();

        dbQuestions[0].Id.Should().Be(q3.Id);
        dbQuestions[0].Order.Should().Be(1);
        dbQuestions[1].Id.Should().Be(q1.Id);
        dbQuestions[1].Order.Should().Be(2);
        dbQuestions[2].Id.Should().Be(q2.Id);
        dbQuestions[2].Order.Should().Be(3);

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == quiz.Id.ToString() && a.Action == "AdminReorderQuestions");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkImportQuestionsAsync_ValidCsv_ImportsQuestionsAndReturnsSummary()
    {
        // Arrange
        using var context = CreateDbContext();
        var quiz = await CreateTestQuizAsync(context, isPublished: false);
        var service = new QuestionService(context, _currentUserMock.Object, _importService);

        var csvContent = new StringBuilder();
        csvContent.AppendLine("Text,Type,Marks,Option1,Option2,Option3,Option4,CorrectOptions");
        csvContent.AppendLine("What is capital of France?,SingleChoice,2,Paris,London,Rome,Berlin,1");
        csvContent.AppendLine("Select all prime numbers,MultiChoice,3,2,3,4,6,1|2");
        csvContent.AppendLine("Water boils at 100C.,TrueFalse,1,True,False,,,True");

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent.ToString()));

        // Act
        var result = await service.BulkImportQuestionsAsync(quiz.Id, stream, "sample_quiz.csv");

        // Assert
        result.TotalImported.Should().Be(3);
        result.Errors.Should().BeEmpty();

        var importedQuestions = await context.Questions
            .Include(q => q.Options)
            .Where(q => q.QuizId == quiz.Id)
            .OrderBy(q => q.Order)
            .ToListAsync();

        importedQuestions.Should().HaveCount(3);
        importedQuestions[0].Text.Should().Be("What is capital of France?");
        importedQuestions[0].Options.Single(o => o.IsCorrect).Text.Should().Be("Paris");

        importedQuestions[1].Text.Should().Be("Select all prime numbers");
        importedQuestions[1].Options.Count(o => o.IsCorrect).Should().Be(2);

        importedQuestions[2].Text.Should().Be("Water boils at 100C.");
        importedQuestions[2].Options.Single(o => o.IsCorrect).Text.Should().Be("True");

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == quiz.Id.ToString() && a.Action == "AdminBulkImportQuestions");
        audit.Should().NotBeNull();
    }
}
