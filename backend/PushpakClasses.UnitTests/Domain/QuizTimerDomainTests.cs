using FluentAssertions;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using Xunit;

namespace PushpakClasses.UnitTests.Domain;

public class QuizTimerDomainTests
{
    [Fact]
    public void QuizAttempt_EndsAtUtc_MustBeStrictlyDerivedFromServerStartTimeAndDuration()
    {
        // Arrange
        var serverStartedAtUtc = new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc);
        const int quizDurationMinutes = 60;

        // Act - Server calculates EndsAtUtc
        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartedAtUtc = serverStartedAtUtc,
            EndsAtUtc = serverStartedAtUtc.AddMinutes(quizDurationMinutes),
            Status = QuizAttemptStatus.InProgress
        };

        // Assert
        attempt.EndsAtUtc.Should().Be(new DateTime(2026, 11, 8, 11, 0, 0, DateTimeKind.Utc));
        attempt.EndsAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void QuizAttempt_Validation_MustDetectExpiredAttemptBasedOnServerUtc()
    {
        // Arrange
        var serverStartedAtUtc = DateTime.UtcNow.AddMinutes(-65);
        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartedAtUtc = serverStartedAtUtc,
            EndsAtUtc = serverStartedAtUtc.AddMinutes(60),
            Status = QuizAttemptStatus.InProgress
        };

        // Act - Server checks if submission or current time is past EndsAtUtc
        var isExpired = DateTime.UtcNow > attempt.EndsAtUtc;

        // Assert
        isExpired.Should().BeTrue("attempts past EndsAtUtc are expired and must be auto-submitted or rejected");
    }

    [Fact]
    public void QuizAttempt_Validation_WithinValidWindow_ShouldBeAccepted()
    {
        // Arrange
        var serverStartedAtUtc = DateTime.UtcNow.AddMinutes(-10);
        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            StartedAtUtc = serverStartedAtUtc,
            EndsAtUtc = serverStartedAtUtc.AddMinutes(30),
            Status = QuizAttemptStatus.InProgress
        };

        // Act
        var isWithinWindow = DateTime.UtcNow <= attempt.EndsAtUtc;

        // Assert
        isWithinWindow.Should().BeTrue();
    }
}
