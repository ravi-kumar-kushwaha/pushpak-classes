using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Reports;

public class PdfCertificateServiceTests
{
    private PushpakClassesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PushpakClassesDbContext(options);
    }

    [Fact]
    public async Task GenerateCertificateAsync_WhenAttemptNotPassed_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();

        var user = new User { Id = Guid.NewGuid(), FullName = "Failing Student", Email = "fail@test.com", Role = UserRole.User, PasswordHash = "h" };
        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "Physics I", CategoryId = Guid.NewGuid(), DurationMinutes = 30, StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(1), IsPublished = true, IsDeleted = false };
        await context.Users.AddAsync(user);
        await context.Quizzes.AddAsync(quiz);

        var failedAttempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = user.Id,
            Status = QuizAttemptStatus.Submitted,
            Percent = 45,
            Passed = false // FAILED!
        };
        await context.QuizAttempts.AddAsync(failedAttempt);
        await context.SaveChangesAsync();

        var service = new PdfCertificateService(context);

        // Act & Assert
        var act = () => service.GenerateCertificateAsync(failedAttempt.Id);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("only available for passed quiz attempts");
    }

    [Fact]
    public async Task GenerateCertificateAsync_WhenAttemptPassed_GeneratesValidPdfBytes()
    {
        // Arrange
        using var context = CreateDbContext();

        var user = new User { Id = Guid.NewGuid(), FullName = "Champion Student", Email = "champ@test.com", Role = UserRole.User, PasswordHash = "h" };
        var quiz = new Quiz { Id = Guid.NewGuid(), Title = "Advanced Algorithms", CategoryId = Guid.NewGuid(), DurationMinutes = 60, StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(2), IsPublished = true, IsDeleted = false };
        await context.Users.AddAsync(user);
        await context.Quizzes.AddAsync(quiz);

        var passedAttempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = user.Id,
            Status = QuizAttemptStatus.Submitted,
            Score = 98,
            TotalMarks = 100,
            Percent = 98,
            Passed = true, // PASSED!
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-40),
            SubmittedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        await context.QuizAttempts.AddAsync(passedAttempt);
        await context.SaveChangesAsync();

        var service = new PdfCertificateService(context);

        // Act
        var (fileBytes, fileName) = await service.GenerateCertificateAsync(passedAttempt.Id);

        // Assert
        fileBytes.Should().NotBeNull();
        fileBytes.Length.Should().BeGreaterThan(0);

        // PDF files always start with %PDF
        var header = System.Text.Encoding.ASCII.GetString(fileBytes.Take(4).ToArray());
        header.Should().Be("%PDF");

        fileName.Should().Contain("Certificate_Advanced_Algorithms_").And.EndWith(".pdf");
    }
}
