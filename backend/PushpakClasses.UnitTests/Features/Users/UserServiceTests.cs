using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Features.Users.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Users;

public class UserServiceTests
{
    private readonly PasswordHasherService _hasher = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IWebHostEnvironment> _envMock = new();

    private PushpakClassesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PushpakClassesDbContext(options);
    }

    [Fact]
    public async Task GetCurrentProfileAsync_ReturnsAuthenticatedUserProfile()
    {
        // Arrange
        using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FullName = "Jane Doe",
            Email = "jane@pushpakclasses.com",
            PhoneNumber = "+919876543210",
            Role = UserRole.User,
            IsActive = true,
            IsEmailVerified = true
        };
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        _currentUserMock.Setup(c => c.UserId).Returns(userId);
        var fileStorage = new LocalFileStorageService(_envMock.Object, NullLogger<LocalFileStorageService>.Instance);
        var userService = new UserService(context, _currentUserMock.Object, _hasher, fileStorage);

        // Act
        var result = await userService.GetCurrentProfileAsync();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.FullName.Should().Be("Jane Doe");
        result.Email.Should().Be("jane@pushpakclasses.com");
        result.PhoneNumber.Should().Be("+919876543210");
        result.Role.Should().Be("User");
    }

    [Fact]
    public async Task UpdateCurrentProfileAsync_UpdatesNameAndPhone()
    {
        // Arrange
        using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FullName = "Original Name",
            Email = "original@pushpakclasses.com",
            PhoneNumber = "1111111111"
        };
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        _currentUserMock.Setup(c => c.UserId).Returns(userId);
        var fileStorage = new LocalFileStorageService(_envMock.Object, NullLogger<LocalFileStorageService>.Instance);
        var userService = new UserService(context, _currentUserMock.Object, _hasher, fileStorage);

        // Act
        var result = await userService.UpdateCurrentProfileAsync(new UpdateProfileRequestDto
        {
            FullName = "Updated Name",
            PhoneNumber = "+919999988888"
        });

        // Assert
        result.FullName.Should().Be("Updated Name");
        result.PhoneNumber.Should().Be("+919999988888");

        var updated = await context.Users.FindAsync(userId);
        updated!.FullName.Should().Be("Updated Name");
        updated.PhoneNumber.Should().Be("+919999988888");
    }

    [Fact]
    public async Task ChangePasswordAsync_WithValidCurrentPassword_UpdatesHashAndResetsLockout()
    {
        // Arrange
        using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "user@pushpakclasses.com",
            FailedLoginCount = 2
        };
        user.PasswordHash = _hasher.HashPassword(user, "CurrentSecret@123");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        _currentUserMock.Setup(c => c.UserId).Returns(userId);
        var fileStorage = new LocalFileStorageService(_envMock.Object, NullLogger<LocalFileStorageService>.Instance);
        var userService = new UserService(context, _currentUserMock.Object, _hasher, fileStorage);

        // Act
        var result = await userService.ChangePasswordAsync(new ChangePasswordRequestDto
        {
            CurrentPassword = "CurrentSecret@123",
            NewPassword = "NewStrongPassword#2026",
            ConfirmNewPassword = "NewStrongPassword#2026"
        });

        // Assert
        result.Message.Should().Contain("Password changed successfully");

        var updated = await context.Users.FindAsync(userId);
        _hasher.VerifyPassword(updated!, updated!.PasswordHash, "NewStrongPassword#2026").Should().BeTrue();
        _hasher.VerifyPassword(updated, updated.PasswordHash, "CurrentSecret@123").Should().BeFalse();
        updated.FailedLoginCount.Should().Be(0);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithInvalidCurrentPassword_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "user@pushpakclasses.com" };
        user.PasswordHash = _hasher.HashPassword(user, "RealPassword@123");
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        _currentUserMock.Setup(c => c.UserId).Returns(userId);
        var fileStorage = new LocalFileStorageService(_envMock.Object, NullLogger<LocalFileStorageService>.Instance);
        var userService = new UserService(context, _currentUserMock.Object, _hasher, fileStorage);

        // Act
        var act = async () => await userService.ChangePasswordAsync(new ChangePasswordRequestDto
        {
            CurrentPassword = "WrongPassword@123",
            NewPassword = "NewPassword#2026",
            ConfirmNewPassword = "NewPassword#2026"
        });

        // Assert
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("Incorrect current password");
    }

    [Fact]
    public async Task UploadProfilePhoto_FileSizeOver2MB_ThrowsBadRequest()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempFolder);
        _envMock.Setup(e => e.WebRootPath).Returns(tempFolder);

        var fileStorage = new LocalFileStorageService(_envMock.Object, NullLogger<LocalFileStorageService>.Instance);
        var userId = Guid.NewGuid();

        // 3 MB byte stream
        var oversizedStream = new MemoryStream(new byte[3 * 1024 * 1024]);

        // Act
        var act = async () => await fileStorage.SaveProfileImageAsync(
            userId,
            oversizedStream,
            "large_avatar.jpg",
            "image/jpeg");

        // Assert
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.Message.Should().Contain("exceeds maximum allowed limit of 2 MB");

        if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
    }

    [Fact]
    public async Task UploadProfilePhoto_InvalidExtension_ThrowsBadRequest()
    {
        // Arrange
        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempFolder);
        _envMock.Setup(e => e.WebRootPath).Returns(tempFolder);

        var fileStorage = new LocalFileStorageService(_envMock.Object, NullLogger<LocalFileStorageService>.Instance);
        var userId = Guid.NewGuid();

        var stream = new MemoryStream(Encoding.UTF8.GetBytes("Fake file content"));

        // Act - Disallowed .pdf extension
        var act = async () => await fileStorage.SaveProfileImageAsync(
            userId,
            stream,
            "document.pdf",
            "application/pdf");

        // Assert
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.Message.Should().Contain("Only JPG, JPEG, and PNG images are allowed");

        if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
    }

    [Fact]
    public async Task RemoveProfilePhotoAsync_RemovesUserPhotoSuccessfully()
    {
        // Arrange
        using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FullName = "Jane Doe",
            Email = "jane@pushpakclasses.com",
            ProfileImageUrl = "/uploads/profiles/test.jpg",
            Role = UserRole.User,
            IsActive = true,
            IsEmailVerified = true
        };
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        _currentUserMock.Setup(u => u.UserId).Returns(userId);
        var fileStorage = new LocalFileStorageService(_envMock.Object, NullLogger<LocalFileStorageService>.Instance);
        var userService = new UserService(context, _currentUserMock.Object, _hasher, fileStorage);

        // Act
        await userService.RemoveProfilePhotoAsync();

        // Assert
        var updated = await context.Users.FindAsync(userId);
        updated!.ProfileImageUrl.Should().BeNull();
    }
}
