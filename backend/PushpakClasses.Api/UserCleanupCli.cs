using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Infrastructure.Persistence;

namespace PushpakClasses.Api;

public static class UserCleanupCli
{
    public static HashSet<string> GetProtectedEmails(IConfiguration? config = null)
    {
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Read from environment variable CLEANUP_PROTECTED_EMAILS
        var envEmails = Environment.GetEnvironmentVariable("CLEANUP_PROTECTED_EMAILS");
        if (!string.IsNullOrWhiteSpace(envEmails))
        {
            foreach (var email in envEmails.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                emails.Add(email);
            }
        }

        // 2. Read from configuration if provided
        if (config != null)
        {
            var configEmails = config.GetSection("CleanupSettings:ProtectedEmails").Get<string[]>();
            if (configEmails != null)
            {
                foreach (var email in configEmails)
                {
                    if (!string.IsNullOrWhiteSpace(email)) emails.Add(email.Trim());
                }
            }

            var adminEmail = config["SeedData:AdminUser:Email"];
            if (!string.IsNullOrWhiteSpace(adminEmail))
            {
                emails.Add(adminEmail.Trim());
            }
        }

        // 3. Fallback safe defaults if none configured
        if (emails.Count == 0)
        {
            emails.Add("admin@example.com");
            emails.Add("candidate@example.com");
        }

        return emails;
    }

    public static bool IsRequested(string[] args)
    {
        return args.Any(a => string.Equals(a, "cleanup-users", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(a, "--cleanup-users", StringComparison.OrdinalIgnoreCase));
    }

    public static async Task ExecuteAsync(WebApplication app, string[] args)
    {
        Console.WriteLine();
        Console.WriteLine("===================================================================");
        Console.WriteLine("          PUSHPAK CLASSES - USER CLEANUP CLI TOOL                  ");
        Console.WriteLine("===================================================================");

        // Security Guard: Environment Check
        if (!app.Environment.IsDevelopment())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: This command is ONLY permitted in the Development environment.");
            Console.WriteLine($"Current Environment: {app.Environment.EnvironmentName}");
            Console.ResetColor();
            Environment.Exit(1);
            return;
        }

        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PushpakClassesDbContext>();
        var protectedEmails = GetProtectedEmails(app.Configuration);

        // STEP 1: Dry Run
        Console.WriteLine("\n[STEP 1/3] Scanning database for users eligible for deletion...");
        Console.WriteLine("Protected accounts (will NEVER be deleted):");
        foreach (var p in protectedEmails)
        {
            Console.WriteLine($"  - {p}");
        }

        var allUsers = await context.Users
            .AsNoTracking()
            .ToListAsync();

        var candidates = allUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.Email) && !protectedEmails.Contains(u.Email.Trim()))
            .ToList();

        if (candidates.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nNo candidate users found for deletion. Database contains only protected accounts.");
            Console.ResetColor();
            return;
        }

        var candidateIds = candidates.Select(u => u.Id).ToList();

        // Get attempt counts per candidate
        var attemptCounts = await context.QuizAttempts
            .AsNoTracking()
            .Where(a => candidateIds.Contains(a.UserId))
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\nFound {candidates.Count} user(s) targeted for deletion:");
        Console.ResetColor();

        Console.WriteLine("-----------------------------------------------------------------------------------------------------------");
        Console.WriteLine($"{"Email",-35} | {"Role",-12} | {"Created (UTC)",-22} | {"Attempts",-8}");
        Console.WriteLine("-----------------------------------------------------------------------------------------------------------");
        foreach (var user in candidates)
        {
            var attempts = attemptCounts.TryGetValue(user.Id, out var count) ? count : 0;
            Console.WriteLine($"{user.Email,-35} | {user.Role,-12} | {user.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC | {attempts,-8}");
        }
        Console.WriteLine("-----------------------------------------------------------------------------------------------------------");

        // STEP 2: Database Backup Snapshot
        Console.WriteLine("\n[STEP 2/3] Generating database backup snapshot before proceeding...");
        var backupDirectory = Path.Combine(app.Environment.ContentRootPath, "backups");
        if (!Directory.Exists(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupFilePath = Path.Combine(backupDirectory, $"cleanup_users_backup_{timestamp}.json");

        // Fetch related data for backup
        var candidateAttempts = await context.QuizAttempts
            .AsNoTracking()
            .Where(a => candidateIds.Contains(a.UserId))
            .ToListAsync();

        var candidateAttemptIds = candidateAttempts.Select(a => a.Id).ToList();

        var candidateAnswers = await context.AttemptAnswers
            .AsNoTracking()
            .Where(aa => candidateAttemptIds.Contains(aa.AttemptId))
            .ToListAsync();

        var candidateReattempts = await context.ReattemptRequests
            .AsNoTracking()
            .Where(r => candidateIds.Contains(r.UserId))
            .ToListAsync();

        var candidateRefreshTokens = await context.RefreshTokens
            .AsNoTracking()
            .Where(rt => candidateIds.Contains(rt.UserId))
            .ToListAsync();

        var candidateEmailTokens = await context.EmailTokens
            .AsNoTracking()
            .Where(et => candidateIds.Contains(et.UserId))
            .ToListAsync();

        var backupData = new
        {
            CreatedAtUtc = DateTime.UtcNow,
            Environment = app.Environment.EnvironmentName,
            ProtectedAccounts = protectedEmails,
            TotalCandidates = candidates.Count,
            Users = candidates,
            QuizAttempts = candidateAttempts,
            AttemptAnswers = candidateAnswers,
            ReattemptRequests = candidateReattempts,
            RefreshTokens = candidateRefreshTokens,
            EmailTokens = candidateEmailTokens
        };

        var json = JsonSerializer.Serialize(backupData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(backupFilePath, json);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Backup successfully created at: {backupFilePath}");
        Console.ResetColor();

        // STEP 3: Explicit Confirmation Prompt
        Console.WriteLine("\n[STEP 3/3] Confirmation Required");
        bool autoConfirm = args.Any(a => a.Equals("--confirm", StringComparison.OrdinalIgnoreCase) || a.Equals("-y", StringComparison.OrdinalIgnoreCase));

        if (!autoConfirm)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("Type 'YES' to permanently delete these users and all linked records: ");
            Console.ResetColor();

            var confirmation = Console.ReadLine();
            if (confirmation?.Trim() != "YES")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Deletion aborted by user. No database changes were made.");
                Console.ResetColor();
                return;
            }
        }
        else
        {
            Console.WriteLine("Confirmation flag provided (--confirm). Proceeding with deletion...");
        }

        // STEP 4: Transactional Cascade Deletion
        Console.WriteLine("\nExecuting transactional deletion in foreign key order...");
        var strategy = context.Database.CreateExecutionStrategy();

        int deletedUsersCount = 0;
        int deletedAttemptsCount = 0;
        int deletedAnswersCount = 0;
        int deletedReattemptsCount = 0;
        int deletedRefreshTokensCount = 0;
        int deletedEmailTokensCount = 0;
        int deletedAuditLogsCount = 0;

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. AttemptAnswers
                var answersToDelete = await context.AttemptAnswers
                    .Where(aa => candidateAttemptIds.Contains(aa.AttemptId))
                    .ToListAsync();
                deletedAnswersCount = answersToDelete.Count;
                context.AttemptAnswers.RemoveRange(answersToDelete);

                // 2. QuizAttempts
                var attemptsToDelete = await context.QuizAttempts
                    .Where(a => candidateIds.Contains(a.UserId))
                    .ToListAsync();
                deletedAttemptsCount = attemptsToDelete.Count;
                context.QuizAttempts.RemoveRange(attemptsToDelete);

                // 3. ReattemptRequests
                var reattemptsToDelete = await context.ReattemptRequests
                    .Where(r => candidateIds.Contains(r.UserId))
                    .ToListAsync();
                deletedReattemptsCount = reattemptsToDelete.Count;
                context.ReattemptRequests.RemoveRange(reattemptsToDelete);

                // 4. RefreshTokens
                var refreshTokensToDelete = await context.RefreshTokens
                    .Where(rt => candidateIds.Contains(rt.UserId))
                    .ToListAsync();
                deletedRefreshTokensCount = refreshTokensToDelete.Count;
                context.RefreshTokens.RemoveRange(refreshTokensToDelete);

                // 5. EmailTokens
                var emailTokensToDelete = await context.EmailTokens
                    .Where(et => candidateIds.Contains(et.UserId))
                    .ToListAsync();
                deletedEmailTokensCount = emailTokensToDelete.Count;
                context.EmailTokens.RemoveRange(emailTokensToDelete);

                // 6. AuditLogs (Only for deleted users)
                var candidateIdsStr = candidateIds.Select(id => id.ToString()).ToList();
                var auditLogsToDelete = await context.AuditLogs
                    .Where(al => (al.UserId.HasValue && candidateIds.Contains(al.UserId.Value))
                              || (al.EntityId != null && candidateIdsStr.Contains(al.EntityId)))
                    .ToListAsync();
                deletedAuditLogsCount = auditLogsToDelete.Count;
                context.AuditLogs.RemoveRange(auditLogsToDelete);

                // 7. Users
                var usersToDelete = await context.Users
                    .Where(u => candidateIds.Contains(u.Id))
                    .ToListAsync();
                deletedUsersCount = usersToDelete.Count;
                context.Users.RemoveRange(usersToDelete);

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nERROR: Deletion transaction failed and was rolled back. Exception: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        });

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n===================================================================");
        Console.WriteLine("                    CLEANUP COMPLETED SUCCESSFULLY                  ");
        Console.WriteLine("===================================================================");
        Console.ResetColor();
        Console.WriteLine($"  - Deleted Users:                 {deletedUsersCount}");
        Console.WriteLine($"  - Deleted Quiz Attempts:         {deletedAttemptsCount}");
        Console.WriteLine($"  - Deleted Attempt Answers:       {deletedAnswersCount}");
        Console.WriteLine($"  - Deleted Reattempt Requests:    {deletedReattemptsCount}");
        Console.WriteLine($"  - Deleted Refresh Tokens:        {deletedRefreshTokensCount}");
        Console.WriteLine($"  - Deleted Email Tokens:          {deletedEmailTokensCount}");
        Console.WriteLine($"  - Deleted Audit Logs:            {deletedAuditLogsCount}");
        Console.WriteLine("===================================================================\n");
    }
}
