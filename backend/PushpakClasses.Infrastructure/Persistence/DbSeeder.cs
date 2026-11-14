using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Infrastructure.Persistence;

public class DbSeeder
{
    private readonly PushpakClassesDbContext _context;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly SeedSettings _seedSettings;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(
        PushpakClassesDbContext context,
        IPasswordHasherService passwordHasher,
        IOptions<SeedSettings> seedSettings,
        ILogger<DbSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _seedSettings = seedSettings.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var adminConfig = _seedSettings.AdminUser;
            var adminEmail = string.IsNullOrWhiteSpace(adminConfig.Email)
                ? "admin@example.com"
                : adminConfig.Email.Trim().ToLowerInvariant();

            var defaultEmails = new List<string> { adminEmail };
            if (!defaultEmails.Contains("admin@pushpakclasses.com", StringComparer.OrdinalIgnoreCase))
            {
                defaultEmails.Add("admin@pushpakclasses.com");
            }

            foreach (var targetEmail in defaultEmails)
            {
                var exists = await _context.Users
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.Email == targetEmail, cancellationToken);

                if (!exists)
                {
                    _logger.LogInformation("Seeding default Admin user ({Email}) for PushpakClasses...", targetEmail);

                    var adminUser = new User
                    {
                        Id = Guid.NewGuid(),
                        FullName = string.IsNullOrWhiteSpace(adminConfig.FullName) ? "PushpakClasses Administrator" : adminConfig.FullName,
                        Email = targetEmail,
                        PhoneNumber = adminConfig.PhoneNumber,
                        Role = UserRole.Admin,
                        IsActive = true,
                        IsEmailVerified = true,
                        FailedLoginCount = 0,
                        CreatedAtUtc = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    var password = string.IsNullOrWhiteSpace(adminConfig.Password) ? "ChangeMe123!" : adminConfig.Password;
                    adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, password);

                    await _context.Users.AddAsync(adminUser, cancellationToken);

                    var auditLog = new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        UserId = adminUser.Id,
                        Action = "SeedDefaultAdmin",
                        EntityName = nameof(User),
                        EntityId = adminUser.Id.ToString(),
                        DetailsJson = $"{{\"Email\":\"{adminUser.Email}\",\"Role\":\"{adminUser.Role}\"}}",
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    await _context.AuditLogs.AddAsync(auditLog, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Successfully seeded default Admin user ({Email}) for PushpakClasses.", targetEmail);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database for PushpakClasses.");
            throw;
        }
    }
}
