using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Domain.Enums;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Persistence;

public class DbSeederTests
{
    private PushpakClassesDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PushpakClassesDbContext(options);
    }

    [Fact]
    public async Task SeedAsync_ShouldCreateAdminUserWhenDatabaseIsEmpty()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        var seedOptions = Options.Create(new SeedSettings
        {
            AdminUser = new AdminUserSeed
            {
                Email = "admin@example.com",
                Password = "ChangeMe123!",
                FullName = "Pushpak Administrator",
                PhoneNumber = "+15555555555"
            }
        });
        var seeder = new DbSeeder(context, hasher, seedOptions, NullLogger<DbSeeder>.Instance);

        // Act
        await seeder.SeedAsync();

        // Assert
        var admin = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@example.com");
        admin.Should().NotBeNull();
        admin!.FullName.Should().Be("Pushpak Administrator");
        admin.Role.Should().Be(UserRole.Admin);
        admin.IsActive.Should().BeTrue();
        admin.IsEmailVerified.Should().BeTrue();
        admin.IsDeleted.Should().BeFalse();

        // Verify password
        hasher.VerifyPassword(admin, admin.PasswordHash, "ChangeMe123!").Should().BeTrue();

        // Verify audit log
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.UserId == admin.Id);
        audit.Should().NotBeNull();
        audit!.Action.Should().Be("SeedDefaultAdmin");
    }

    [Fact]
    public async Task SeedAsync_ShouldBeIdempotent_WhenCalledMultipleTimes()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasherService();
        var seedOptions = Options.Create(new SeedSettings
        {
            AdminUser = new AdminUserSeed
            {
                Email = "admin@example.com",
                Password = "ChangeMe123!"
            }
        });
        var seeder = new DbSeeder(context, hasher, seedOptions, NullLogger<DbSeeder>.Instance);

        // Act
        await seeder.SeedAsync();
        await seeder.SeedAsync(); // Second run

        // Assert
        var adminCount = await context.Users.CountAsync(u => u.Email == "admin@example.com");
        adminCount.Should().Be(1, "running seeder multiple times must not create duplicate admin accounts");
    }
}
