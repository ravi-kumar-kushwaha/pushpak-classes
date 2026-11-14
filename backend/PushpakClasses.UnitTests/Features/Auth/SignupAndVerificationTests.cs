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
using PushpakClasses.Application.Features.Auth.Validators;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Auth;

public class SignupAndVerificationTests
{
    private readonly PasswordHasherService _hasher = new();
    private readonly JwtTokenService _jwtService;
    private readonly Mock<IEmailService> _emailMock = new();
    private readonly IOptions<EmailSettings> _emailOptions = Options.Create(new EmailSettings());

    public SignupAndVerificationTests()
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
    public async Task Signup_RoleIsAlwaysUser_AndGeneratesVerificationToken()
    {
        // Arrange
        using var context = CreateDbContext();
        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act
        var result = await authService.SignupAsync(new SignupRequestDto
        {
            FullName = "New Candidate",
            Email = "candidate@pushpakclasses.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            PhoneNumber = "+919999988888"
        });

        // Assert
        result.Message.Should().Contain("Registration successful");

        var createdUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "candidate@pushpakclasses.com");
        createdUser.Should().NotBeNull();
        createdUser!.Role.Should().Be(UserRole.User, "self-signup must ALWAYS enforce User role");
        createdUser.IsActive.Should().BeTrue();
        createdUser.IsEmailVerified.Should().BeFalse();

        var token = await context.EmailTokens.FirstOrDefaultAsync(t => t.UserId == createdUser.Id && t.Type == EmailTokenType.Verify);
        token.Should().NotBeNull();
        token!.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow.AddHours(23));

        _emailMock.Verify(e => e.SendEmailAsync(
            createdUser.Email,
            It.Is<string>(s => s.Contains("Verify Your Email")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyEmail_WithValidToken_MarksUserEmailVerified()
    {
        // Arrange
        using var context = CreateDbContext();
        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        await authService.SignupAsync(new SignupRequestDto
        {
            FullName = "Verification Candidate",
            Email = "verify@pushpakclasses.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123"
        });

        var user = await context.Users.FirstAsync(u => u.Email == "verify@pushpakclasses.com");
        var token = await context.EmailTokens.FirstAsync(t => t.UserId == user.Id && t.Type == EmailTokenType.Verify);

        // Act
        var result = await authService.VerifyEmailAsync(new VerifyEmailRequestDto
        {
            Email = "verify@pushpakclasses.com",
            Token = token.Token
        });

        // Assert
        result.Message.Should().Contain("Email verified successfully");

        var verifiedUser = await context.Users.FindAsync(user.Id);
        verifiedUser!.IsEmailVerified.Should().BeTrue();

        var usedToken = await context.EmailTokens.FindAsync(token.Id);
        usedToken!.UsedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Signup_DuplicateEmail_ActiveUser_ThrowsConflict409()
    {
        // Arrange
        using var context = CreateDbContext();
        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Existing User",
            Email = "existing@pushpakclasses.com",
            PasswordHash = "hash",
            Role = UserRole.User,
            IsActive = true,
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        await context.Users.AddAsync(existingUser);
        await context.SaveChangesAsync();

        // Act
        var act = async () => await authService.SignupAsync(new SignupRequestDto
        {
            FullName = "Another User",
            Email = "existing@pushpakclasses.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123"
        });

        // Assert
        var exception = await act.Should().ThrowAsync<AuthException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Conflict);
        exception.Which.Message.Should().Contain("An account with this email address already exists.");
    }

    [Fact]
    public async Task Signup_DuplicateEmail_SoftDeletedUser_AllowsReactivation()
    {
        // Arrange
        using var context = CreateDbContext();
        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        var softDeletedUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Old Deleted User",
            Email = "rejoin@pushpakclasses.com",
            PasswordHash = "old_hash",
            Role = UserRole.User,
            IsActive = false,
            IsDeleted = true, // Soft deleted!
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-3)
        };
        await context.Users.AddAsync(softDeletedUser);
        await context.SaveChangesAsync();

        // Act
        var result = await authService.SignupAsync(new SignupRequestDto
        {
            FullName = "Reactivated User",
            Email = "rejoin@pushpakclasses.com",
            Password = "NewPassword@123",
            ConfirmPassword = "NewPassword@123"
        });

        // Assert
        result.Message.Should().Contain("Registration successful");

        var reactivatedUser = await context.Users.FirstAsync(u => u.Email == "rejoin@pushpakclasses.com");
        reactivatedUser.IsDeleted.Should().BeFalse();
        reactivatedUser.IsActive.Should().BeTrue();
        reactivatedUser.IsEmailVerified.Should().BeFalse();
        reactivatedUser.FullName.Should().Be("Reactivated User");
    }

    [Fact]
    public async Task VerifyEmail_TokenOnly_WithoutEmail_Succeeds()
    {
        // Arrange
        using var context = CreateDbContext();
        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        await authService.SignupAsync(new SignupRequestDto
        {
            FullName = "Token Only Candidate",
            Email = "tokenonly@pushpakclasses.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123"
        });

        var user = await context.Users.FirstAsync(u => u.Email == "tokenonly@pushpakclasses.com");
        var token = await context.EmailTokens.FirstAsync(t => t.UserId == user.Id && t.Type == EmailTokenType.Verify);

        // Act - Only passing Token (no Email in request)
        var result = await authService.VerifyEmailAsync(new VerifyEmailRequestDto
        {
            Token = token.Token
        });

        // Assert
        result.Message.Should().Contain("Email verified successfully");

        var verifiedUser = await context.Users.FindAsync(user.Id);
        verifiedUser!.IsEmailVerified.Should().BeTrue();
    }

    [Theory]
    [InlineData("short", "Password must be at least 8 characters long.")]
    [InlineData("nouppercase123!", "Password must contain at least one uppercase letter.")]
    [InlineData("NOLOWERCASE123!", "Password must contain at least one lowercase letter.")]
    [InlineData("NoDigitSpecial!", "Password must contain at least one digit.")]
    [InlineData("NoSpecial1234", "Password must contain at least one special character.")]
    public async Task SignupRequestValidator_WeakPasswords_FailValidation(string weakPassword, string expectedError)
    {
        // Arrange
        var validator = new SignupRequestValidator();
        var request = new SignupRequestDto
        {
            FullName = "John Tester",
            Email = "tester@pushpakclasses.com",
            Password = weakPassword,
            ConfirmPassword = weakPassword
        };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains(expectedError));
    }

    [Fact]
    public async Task SignupRequestValidator_PasswordMismatch_FailsValidation()
    {
        // Arrange
        var validator = new SignupRequestValidator();
        var request = new SignupRequestDto
        {
            FullName = "John Tester",
            Email = "tester@pushpakclasses.com",
            Password = "Password@123",
            ConfirmPassword = "DifferentPassword@123"
        };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Passwords do not match."));
    }

    [Fact]
    public async Task SignupRequestValidator_MissingConfirmPassword_FailsValidation()
    {
        // Arrange
        var validator = new SignupRequestValidator();
        var request = new SignupRequestDto
        {
            FullName = "John Tester",
            Email = "tester@pushpakclasses.com",
            Password = "Password@123",
            ConfirmPassword = "" // Missing!
        };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Confirm password is required."));
    }

    [Fact]
    public async Task SignupRequestValidator_StrongPasswordAndMatchingConfirm_PassesValidation()
    {
        // Arrange
        var validator = new SignupRequestValidator();
        var request = new SignupRequestDto
        {
            FullName = "John Tester",
            Email = "tester@pushpakclasses.com",
            Password = "ValidPassword@123!",
            ConfirmPassword = "ValidPassword@123!"
        };

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
