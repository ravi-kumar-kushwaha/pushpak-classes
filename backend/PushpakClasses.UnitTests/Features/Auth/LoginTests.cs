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

public class LoginTests
{
    private readonly PasswordHasherService _hasher = new();
    private readonly JwtTokenService _jwtService;
    private readonly Mock<IEmailService> _emailMock = new();
    private readonly IOptions<EmailSettings> _emailOptions = Options.Create(new EmailSettings());

    public LoginTests()
    {
        var jwtOptions = Options.Create(new JwtSettings
        {
            Secret = "Super_Secret_Testing_Key_At_Least_32_Chars_Long!",
            Issuer = "PushpakClasses.Api",
            Audience = "PushpakClasses.Client",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
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
    public async Task Login_WithValidCredentials_ReturnsAccessTokenAndRefreshToken()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@pushpakclasses.com",
            FullName = "Student One",
            Role = UserRole.User,
            IsActive = true,
            IsEmailVerified = true,
            FailedLoginCount = 0
        };
        user.PasswordHash = _hasher.HashPassword(user, "StrongPass@123");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act
        var result = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "student@pushpakclasses.com",
            Password = "StrongPass@123"
        });

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.User.Email.Should().Be("student@pushpakclasses.com");
        result.User.Role.Should().Be("User");

        // Verify refresh token was saved in database
        var storedToken = await context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == result.RefreshToken);
        storedToken.Should().NotBeNull();
        storedToken!.UserId.Should().Be(user.Id);
        storedToken.RevokedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ThrowsUnauthorizedAndIncrementsFailedCount()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@pushpakclasses.com",
            FullName = "Student One",
            Role = UserRole.User,
            IsActive = true,
            FailedLoginCount = 0
        };
        user.PasswordHash = _hasher.HashPassword(user, "CorrectPass@123");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act
        var act = async () => await authService.LoginAsync(new LoginRequestDto
        {
            Email = "student@pushpakclasses.com",
            Password = "WrongPassword@123"
        });

        // Assert
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var updatedUser = await context.Users.FindAsync(user.Id);
        updatedUser!.FailedLoginCount.Should().Be(1);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ThrowsUnauthorized()
    {
        // Arrange
        using var context = CreateDbContext();
        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act
        var act = async () => await authService.LoginAsync(new LoginRequestDto
        {
            Email = "doesnotexist@pushpakclasses.com",
            Password = "SomePassword@123"
        });

        // Assert
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInactiveUser_ThrowsForbidden()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "disabled@pushpakclasses.com",
            FullName = "Disabled User",
            Role = UserRole.User,
            IsActive = false
        };
        user.PasswordHash = _hasher.HashPassword(user, "Password@123");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act
        var act = async () => await authService.LoginAsync(new LoginRequestDto
        {
            Email = "disabled@pushpakclasses.com",
            Password = "Password@123"
        });

        // Assert
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
