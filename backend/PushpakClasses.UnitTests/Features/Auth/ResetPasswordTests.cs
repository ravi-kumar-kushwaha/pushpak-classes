using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Auth;

public class ResetPasswordTests
{
    private readonly PasswordHasherService _hasher = new();
    private readonly JwtTokenService _jwtService;
    private readonly Mock<IEmailService> _emailMock = new();
    private readonly IOptions<EmailSettings> _emailOptions = Options.Create(new EmailSettings());

    public ResetPasswordTests()
    {
        var jwtOptions = Options.Create(new JwtSettings
        {
            Secret = "Super_Secret_Testing_Key_At_Least_32_Chars_Long!",
            Issuer = "PushpakClasses.Api",
            Audience = "PushpakClasses.Client"
        });
        _jwtService = new JwtTokenService(jwtOptions);
    }

    private PushpakClassesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PushpakClassesDbContext(options);
    }

    [Fact]
    public async Task ForgotPassword_AlwaysReturnsGenericSuccess_ForBothExistingAndNonExistingEmails()
    {
        // Arrange
        using var context = CreateDbContext();
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "registered@pushpakclasses.com",
            FullName = "Registered User",
            Role = UserRole.User,
            IsActive = true
        };
        await context.Users.AddAsync(existingUser);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act 1: Existing email
        var result1 = await authService.ForgotPasswordAsync(new ForgotPasswordRequestDto
        {
            Email = "registered@pushpakclasses.com"
        });

        // Act 2: Non-existing email
        var result2 = await authService.ForgotPasswordAsync(new ForgotPasswordRequestDto
        {
            Email = "unregistered@pushpakclasses.com"
        });

        // Assert - Anti-enumeration
        result1.Message.Should().Be(result2.Message);
        result1.Message.Should().Contain("If an account exists with this email");

        // Verify token created for existing user and expires in 30 minutes
        var token = await context.EmailTokens.FirstOrDefaultAsync(t => t.UserId == existingUser.Id && t.Type == EmailTokenType.ResetPassword);
        token.Should().NotBeNull();
        token!.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(29));
        token.ExpiresAtUtc.Should().BeBefore(DateTime.UtcNow.AddMinutes(31));

        // Verify email dispatched
        _emailMock.Verify(e => e.SendEmailAsync(
            existingUser.Email,
            It.Is<string>(s => s.Contains("Password Reset")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_UpdatesPasswordAndRevokesAllRefreshTokens()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "resetme@pushpakclasses.com",
            FullName = "Reset User",
            Role = UserRole.User,
            IsActive = true,
            FailedLoginCount = 3
        };
        user.PasswordHash = _hasher.HashPassword(user, "OldPassword@123");
        await context.Users.AddAsync(user);

        var resetToken = new EmailToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "valid-reset-token-12345",
            Type = EmailTokenType.ResetPassword,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(20), // Valid
            UsedAtUtc = null
        };
        await context.EmailTokens.AddAsync(resetToken);

        var activeRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "active-refresh-token",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            RevokedAtUtc = null
        };
        await context.RefreshTokens.AddAsync(activeRefreshToken);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act
        var result = await authService.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = "resetme@pushpakclasses.com",
            Token = "valid-reset-token-12345",
            NewPassword = "NewStrongPassword@2026",
            ConfirmNewPassword = "NewStrongPassword@2026"
        });

        // Assert
        result.Message.Should().Contain("Password has been reset successfully");

        var updatedUser = await context.Users.FindAsync(user.Id);
        _hasher.VerifyPassword(updatedUser!, updatedUser!.PasswordHash, "NewStrongPassword@2026").Should().BeTrue();
        _hasher.VerifyPassword(updatedUser, updatedUser.PasswordHash, "OldPassword@123").Should().BeFalse();
        updatedUser.FailedLoginCount.Should().Be(0);

        var updatedToken = await context.EmailTokens.FindAsync(resetToken.Id);
        updatedToken!.UsedAtUtc.Should().NotBeNull();

        var updatedRefreshToken = await context.RefreshTokens.FindAsync(activeRefreshToken.Id);
        updatedRefreshToken!.RevokedAtUtc.Should().NotBeNull("resetting password must revoke existing refresh tokens");
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "expiredtoken@pushpakclasses.com",
            FullName = "Expired Token User",
            Role = UserRole.User,
            IsActive = true
        };
        user.PasswordHash = _hasher.HashPassword(user, "OldPass@123");
        await context.Users.AddAsync(user);

        var expiredToken = new EmailToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "expired-token",
            Type = EmailTokenType.ResetPassword,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
            UsedAtUtc = null
        };
        await context.EmailTokens.AddAsync(expiredToken);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act
        var act = async () => await authService.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = "expiredtoken@pushpakclasses.com",
            Token = "expired-token",
            NewPassword = "NewPassword@123",
            ConfirmNewPassword = "NewPassword@123"
        });

        // Assert
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task ResetPassword_WithAlreadyUsedToken_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "usedtoken@pushpakclasses.com",
            FullName = "Used Token User",
            Role = UserRole.User,
            IsActive = true
        };
        user.PasswordHash = _hasher.HashPassword(user, "OldPass@123");
        await context.Users.AddAsync(user);

        var usedToken = new EmailToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "already-used-token",
            Type = EmailTokenType.ResetPassword,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(20),
            UsedAtUtc = DateTime.UtcNow.AddMinutes(-5) // Already used
        };
        await context.EmailTokens.AddAsync(usedToken);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act
        var act = async () => await authService.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = "usedtoken@pushpakclasses.com",
            Token = "already-used-token",
            NewPassword = "NewPassword@123",
            ConfirmNewPassword = "NewPassword@123"
        });

        // Assert
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.Message.Should().Contain("already used");
    }
}
