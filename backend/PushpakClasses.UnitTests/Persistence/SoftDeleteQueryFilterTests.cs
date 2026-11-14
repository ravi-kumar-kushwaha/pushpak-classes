using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using Xunit;

namespace PushpakClasses.UnitTests.Persistence;

public class SoftDeleteQueryFilterTests
{
    private PushpakClassesDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PushpakClassesDbContext(options);
    }

    [Fact]
    public async Task SoftDeletedUser_ShouldBeExcludedFromNormalQuery()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var activeUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "active@pushpakclasses.com",
            FullName = "Active User",
            PasswordHash = "hash1",
            IsDeleted = false
        };
        var deletedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "deleted@pushpakclasses.com",
            FullName = "Deleted User",
            PasswordHash = "hash2",
            IsDeleted = true
        };

        await context.Users.AddRangeAsync(activeUser, deletedUser);
        await context.SaveChangesAsync();

        // Act
        var users = await context.Users.ToListAsync();
        var allUsersWithDeleted = await context.Users.IgnoreQueryFilters().ToListAsync();

        // Assert
        users.Should().ContainSingle(u => u.Email == "active@pushpakclasses.com");
        users.Should().NotContain(u => u.Email == "deleted@pushpakclasses.com");
        allUsersWithDeleted.Should().HaveCount(2);
    }

    [Fact]
    public async Task SoftDeletedQuiz_ShouldBeExcludedFromNormalQuery()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "General Studies" };
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@example.com", FullName = "Admin", PasswordHash = "hash" };
        var activeQuiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Active Quiz",
            CategoryId = category.Id,
            CreatedByUserId = admin.Id,
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddDays(7),
            PassingPercent = 60,
            IsDeleted = false
        };
        var deletedQuiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Deleted Quiz",
            CategoryId = category.Id,
            CreatedByUserId = admin.Id,
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddDays(7),
            PassingPercent = 60,
            IsDeleted = true
        };

        await context.Categories.AddAsync(category);
        await context.Users.AddAsync(admin);
        await context.Quizzes.AddRangeAsync(activeQuiz, deletedQuiz);
        await context.SaveChangesAsync();

        // Act
        var quizzes = await context.Quizzes.ToListAsync();
        var allQuizzes = await context.Quizzes.IgnoreQueryFilters().ToListAsync();

        // Assert
        quizzes.Should().ContainSingle(q => q.Title == "Active Quiz");
        quizzes.Should().NotContain(q => q.Title == "Deleted Quiz");
        allQuizzes.Should().HaveCount(2);
    }

    [Fact]
    public async Task SoftDeletedQuestion_ShouldBeExcludedFromNormalQuery()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Math Quiz",
            DurationMinutes = 15,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddDays(1),
            PassingPercent = 50
        };

        var activeQuestion = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is 2 + 2?",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Order = 1,
            IsDeleted = false
        };
        var deletedQuestion = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            Text = "What is 10 / 0?",
            Type = QuestionType.SingleChoice,
            Marks = 1,
            Order = 2,
            IsDeleted = true
        };

        await context.Quizzes.AddAsync(quiz);
        await context.Questions.AddRangeAsync(activeQuestion, deletedQuestion);
        await context.SaveChangesAsync();

        // Act
        var questions = await context.Questions.ToListAsync();
        var allQuestions = await context.Questions.IgnoreQueryFilters().ToListAsync();

        // Assert
        questions.Should().ContainSingle(q => q.Text == "What is 2 + 2?");
        questions.Should().NotContain(q => q.Text == "What is 10 / 0?");
        allQuestions.Should().HaveCount(2);
    }
}
