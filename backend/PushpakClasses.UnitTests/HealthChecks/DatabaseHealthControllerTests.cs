using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PushpakClasses.Api.Controllers;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using Xunit;

namespace PushpakClasses.UnitTests.HealthChecks;

public class DatabaseHealthControllerTests
{
    private PushpakClassesDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PushpakClassesDbContext(options);
    }

    [Fact]
    public async Task GetDatabaseHealth_ReturnsOkWithServerDatabaseAndCounts()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var category = new Category { Id = Guid.NewGuid(), Name = "Aptitude" };
        var admin = new User { Id = Guid.NewGuid(), FullName = "Admin", Email = "admin@example.com", Role = UserRole.Admin, PasswordHash = "hash" };
        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "Quiz 1", CategoryId = category.Id, CreatedByUserId = admin.Id, DurationMinutes = 30 };
        var attempt = new QuizAttempt { Id = Guid.NewGuid(), QuizId = quiz.Id, UserId = admin.Id, Status = QuizAttemptStatus.Submitted, StartedAtUtc = DateTime.UtcNow };

        await context.Categories.AddAsync(category);
        await context.Users.AddAsync(admin);
        await context.Quizzes.AddAsync(quiz);
        await context.QuizAttempts.AddAsync(attempt);
        await context.SaveChangesAsync();

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:DefaultConnection", "Server=azure-sql.database.windows.net;Database=PushpakClassesDb;User Id=user;Password=pass;" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        var controller = new DatabaseHealthController(context, configuration);

        // Act
        var result = await controller.GetDatabaseHealth(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = okResult.Value.Should().BeOfType<DatabaseHealthResponse>().Subject;

        data.Status.Should().Be("Healthy");
        data.Server.Should().Be("azure-sql.database.windows.net");
        data.Database.Should().Be("PushpakClassesDb");
        data.Counts.Users.Should().Be(1);
        data.Counts.Quizzes.Should().Be(1);
        data.Counts.Categories.Should().Be(1);
        data.Counts.Attempts.Should().Be(1);
    }
}
