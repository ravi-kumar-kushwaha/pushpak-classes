using FluentAssertions;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Services;

public class PasswordHasherServiceTests
{
    private readonly PasswordHasherService _hasher = new();
    private readonly User _testUser = new()
    {
        Id = Guid.NewGuid(),
        Email = "test@pushpakclasses.com"
    };

    [Fact]
    public void HashPassword_ShouldReturnValidNonEmptyHash()
    {
        // Act
        var hash = _hasher.HashPassword(_testUser, "Secret@123");

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        const string password = "PushpakSecurePassword#2026";
        var hash = _hasher.HashPassword(_testUser, password);

        // Act
        var isValid = _hasher.VerifyPassword(_testUser, hash, password);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var hash = _hasher.HashPassword(_testUser, "CorrectPassword@123");

        // Act
        var isValid = _hasher.VerifyPassword(_testUser, hash, "WrongPassword@123");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void HashPassword_ShouldGenerateUniqueSaltPerHash()
    {
        // Arrange
        const string password = "CommonPassword@123";

        // Act
        var hash1 = _hasher.HashPassword(_testUser, password);
        var hash2 = _hasher.HashPassword(_testUser, password);

        // Assert
        hash1.Should().NotBe(hash2, "distinct salts must produce distinct hash outputs");
    }
}
