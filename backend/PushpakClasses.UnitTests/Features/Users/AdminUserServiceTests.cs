using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Users.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Users;

public class AdminUserServiceTests
{
    private readonly PasswordHasherService _hasher = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();

    private PushpakClassesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PushpakClassesDbContext(options);
    }

    [Fact]
    public async Task GetUsersAsync_WithPaginationAndSearch_ReturnsFilteredResult()
    {
        // Arrange
        using var context = CreateDbContext();
        for (int i = 1; i <= 15; i++)
        {
            await context.Users.AddAsync(new User
            {
                Id = Guid.NewGuid(),
                FullName = i % 2 == 0 ? $"Student Alpha {i}" : $"Learner Beta {i}",
                Email = $"user{i}@pushpakclasses.com",
                Role = i <= 5 ? UserRole.Admin : UserRole.User,
                IsActive = i % 3 != 0,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await context.SaveChangesAsync();

        var adminService = new AdminUserService(context, _currentUserMock.Object, _hasher);

        // Act 1: Search by "Alpha"
        var alphaResults = await adminService.GetUsersAsync(new UserQueryParameters
        {
            SearchQuery = "Alpha",
            PageNumber = 1,
            PageSize = 10
        });

        // Assert 1
        alphaResults.Items.Should().OnlyContain(u => u.FullName.Contains("Alpha"));
        alphaResults.TotalCount.Should().Be(7); // 2,4,6,8,10,12,14

        // Act 2: Filter by Role = Admin
        var adminResults = await adminService.GetUsersAsync(new UserQueryParameters
        {
            Role = UserRole.Admin,
            PageNumber = 1,
            PageSize = 10
        });

        // Assert 2
        adminResults.TotalCount.Should().Be(5);
        adminResults.Items.Should().OnlyContain(u => u.Role == "Admin");

        // Act 3: Filter by IsActive = false
        var inactiveResults = await adminService.GetUsersAsync(new UserQueryParameters
        {
            IsActive = false,
            PageNumber = 1,
            PageSize = 10
        });

        // Assert 3
        inactiveResults.Items.Should().OnlyContain(u => !u.IsActive);
    }

    [Fact]
    public async Task CreateUserAsync_CreatesUserWithRoleAndWritesAuditLog()
    {
        // Arrange
        using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        _currentUserMock.Setup(c => c.UserId).Returns(adminId);

        var adminService = new AdminUserService(context, _currentUserMock.Object, _hasher);

        // Act
        var result = await adminService.CreateUserAsync(new AdminCreateUserRequestDto
        {
            FullName = "New Manager",
            Email = "manager@pushpakclasses.com",
            Password = "ManagerPassword@123",
            Role = UserRole.Admin,
            PhoneNumber = "+919876543210",
            IsActive = true
        });

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("manager@pushpakclasses.com");
        result.Role.Should().Be("Admin");

        var user = await context.Users.FindAsync(result.Id);
        user.Should().NotBeNull();
        user!.Role.Should().Be(UserRole.Admin);

        var auditLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "AdminCreateUser" && a.EntityId == result.Id.ToString());
        auditLog.Should().NotBeNull();
        auditLog!.UserId.Should().Be(adminId);
    }

    [Fact]
    public async Task SoftDeleteUserAsync_MarksIsDeletedTrueAndWritesAuditLog()
    {
        // Arrange
        using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _currentUserMock.Setup(c => c.UserId).Returns(adminId);

        var targetUser = new User
        {
            Id = targetUserId,
            FullName = "To Be Deleted",
            Email = "delete@pushpakclasses.com",
            Role = UserRole.User,
            IsDeleted = false
        };
        await context.Users.AddAsync(targetUser);
        await context.SaveChangesAsync();

        var adminService = new AdminUserService(context, _currentUserMock.Object, _hasher);

        // Act
        var result = await adminService.DeleteUserAsync(targetUserId);

        // Assert
        result.Message.Should().Contain("deleted successfully");

        // Normal query excludes soft deleted user (Rule 6)
        var visibleUser = await context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
        visibleUser.Should().BeNull();

        // But user exists when ignoring filters
        var rawUser = await context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == targetUserId);
        rawUser.Should().NotBeNull();
        rawUser!.IsDeleted.Should().BeTrue();

        var auditLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "AdminDeleteUser" && a.EntityId == targetUserId.ToString());
        auditLog.Should().NotBeNull();
        auditLog!.UserId.Should().Be(adminId);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenAdminTriesToDeleteSelf_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        _currentUserMock.Setup(c => c.UserId).Returns(adminId);

        var admin = new User
        {
            Id = adminId,
            FullName = "Admin Self",
            Email = "admin@example.com",
            Role = UserRole.Admin,
            IsDeleted = false
        };
        await context.Users.AddAsync(admin);
        await context.SaveChangesAsync();

        var adminService = new AdminUserService(context, _currentUserMock.Object, _hasher);

        // Act
        var act = async () => await adminService.DeleteUserAsync(adminId);

        // Assert - Admin cannot delete self
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("cannot delete their own account");
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenAdminTriesToDeactivateSelf_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        _currentUserMock.Setup(c => c.UserId).Returns(adminId);

        var admin = new User
        {
            Id = adminId,
            FullName = "Admin Self",
            Email = "admin@example.com",
            Role = UserRole.Admin,
            IsActive = true
        };
        await context.Users.AddAsync(admin);
        await context.SaveChangesAsync();

        var adminService = new AdminUserService(context, _currentUserMock.Object, _hasher);

        // Act - Admin tries to deactivate self
        var act = async () => await adminService.UpdateStatusAsync(adminId, new UpdateUserStatusRequestDto
        {
            IsActive = false
        });

        // Assert
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("cannot deactivate their own account");
    }

    [Fact]
    public async Task UpdateRoleAsync_WhenAdminTriesToDemoteSelf_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        _currentUserMock.Setup(c => c.UserId).Returns(adminId);

        var admin = new User
        {
            Id = adminId,
            FullName = "Admin Self",
            Email = "admin@example.com",
            Role = UserRole.Admin
        };
        await context.Users.AddAsync(admin);
        await context.SaveChangesAsync();

        var adminService = new AdminUserService(context, _currentUserMock.Object, _hasher);

        // Act - Admin tries to demote self to User
        var act = async () => await adminService.UpdateRoleAsync(adminId, new UpdateUserRoleRequestDto
        {
            Role = UserRole.User
        });

        // Assert
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("cannot revoke their own administrator role");
    }

    [Fact]
    public async Task ResetPasswordAsync_SetsTemporaryPasswordAndRevokesRefreshTokens()
    {
        // Arrange
        using var context = CreateDbContext();
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _currentUserMock.Setup(c => c.UserId).Returns(adminId);

        var user = new User
        {
            Id = targetUserId,
            Email = "target@pushpakclasses.com",
            FailedLoginCount = 4
        };
        user.PasswordHash = _hasher.HashPassword(user, "OldPassword@123");
        await context.Users.AddAsync(user);

        var activeRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            Token = "active-token",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            RevokedAtUtc = null
        };
        await context.RefreshTokens.AddAsync(activeRefreshToken);
        await context.SaveChangesAsync();

        var adminService = new AdminUserService(context, _currentUserMock.Object, _hasher);

        // Act
        var result = await adminService.ResetPasswordAsync(targetUserId, new AdminResetPasswordRequestDto
        {
            TemporaryPassword = "TempPassword@2026"
        });

        // Assert
        result.Message.Should().Contain("Temporary password set successfully");

        var updatedUser = await context.Users.FindAsync(targetUserId);
        _hasher.VerifyPassword(updatedUser!, updatedUser!.PasswordHash, "TempPassword@2026").Should().BeTrue();
        updatedUser.FailedLoginCount.Should().Be(0);

        var token = await context.RefreshTokens.FindAsync(activeRefreshToken.Id);
        token!.RevokedAtUtc.Should().NotBeNull("admin reset password must revoke all active refresh tokens");

        var auditLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "AdminResetPassword" && a.EntityId == targetUserId.ToString());
        auditLog.Should().NotBeNull();
    }
}
