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

public class LockoutTests
{
    private readonly PasswordHasherService _hasher = new();
    private readonly JwtTokenService _jwtService;
    private readonly Mock<IEmailService> _emailMock = new();
    private readonly IOptions<EmailSettings> _emailOptions = Options.Create(new EmailSettings());

    public LockoutTests()
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
    public async Task FiveFailedLogins_TriggersFifteenMinuteLockout()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "lockout@pushpakclasses.com",
            FullName = "Lockout Candidate",
            Role = UserRole.User,
            IsActive = true,
            FailedLoginCount = 0,
            LockoutEndUtc = null
        };
        user.PasswordHash = _hasher.HashPassword(user, "CorrectPass@123");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act - 4 failed attempts
        for (int i = 0; i < 4; i++)
        {
            var act = async () => await authService.LoginAsync(new LoginRequestDto
            {
                Email = "lockout@pushpakclasses.com",
                Password = "WrongPassword"
            });
            var ex = await act.Should().ThrowAsync<AuthException>();
            ex.Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var userAfterFour = await context.Users.FindAsync(user.Id);
        userAfterFour!.FailedLoginCount.Should().Be(4);
        userAfterFour.LockoutEndUtc.Should().BeNull();

        // 5th failed attempt -> should trigger lockout
        var fifthAct = async () => await authService.LoginAsync(new LoginRequestDto
        {
            Email = "lockout@pushpakclasses.com",
            Password = "WrongPassword"
        });

        // Assert
        var fifthEx = await fifthAct.Should().ThrowAsync<AuthException>();
        fifthEx.Which.StatusCode.Should().Be((HttpStatusCode)423);

        var lockedUser = await context.Users.FindAsync(user.Id);
        lockedUser!.FailedLoginCount.Should().Be(5);
        lockedUser.LockoutEndUtc.Should().NotBeNull();
        lockedUser.LockoutEndUtc!.Value.Should().BeAfter(DateTime.UtcNow.AddMinutes(14));
        lockedUser.LockoutEndUtc.Value.Should().BeBefore(DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public async Task Login_WhileAccountLocked_ThrowsLockedOut423EvenWithCorrectPassword()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "locked@pushpakclasses.com",
            FullName = "Locked User",
            Role = UserRole.User,
            IsActive = true,
            FailedLoginCount = 5,
            LockoutEndUtc = DateTime.UtcNow.AddMinutes(10) // 10 minutes remaining
        };
        user.PasswordHash = _hasher.HashPassword(user, "CorrectPass@123");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act - Attempt login with correct password during lockout
        var act = async () => await authService.LoginAsync(new LoginRequestDto
        {
            Email = "locked@pushpakclasses.com",
            Password = "CorrectPass@123"
        });

        // Assert
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be((HttpStatusCode)423);
        ex.Which.Message.Should().Contain("locked out");
    }

    [Fact]
    public async Task Login_AfterLockoutExpires_WithCorrectPassword_SucceedsAndClearsLockout()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "expiredlockout@pushpakclasses.com",
            FullName = "Expired Lockout User",
            Role = UserRole.User,
            IsActive = true,
            FailedLoginCount = 5,
            LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1) // Lockout expired 1 minute ago
        };
        user.PasswordHash = _hasher.HashPassword(user, "CorrectPass@123");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        var authService = new AuthService(context, _hasher, _jwtService, _emailMock.Object, _emailOptions, NullLogger<AuthService>.Instance);

        // Act
        var result = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "expiredlockout@pushpakclasses.com",
            Password = "CorrectPass@123"
        });

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrWhiteSpace();

        var clearedUser = await context.Users.FindAsync(user.Id);
        clearedUser!.FailedLoginCount.Should().Be(0);
        clearedUser.LockoutEndUtc.Should().BeNull();
    }
}
