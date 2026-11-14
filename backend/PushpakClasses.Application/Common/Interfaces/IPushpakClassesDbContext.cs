using Microsoft.EntityFrameworkCore;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Application.Common.Interfaces;

public interface IPushpakClassesDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<EmailToken> EmailTokens { get; }
    DbSet<Category> Categories { get; }
    DbSet<Quiz> Quizzes { get; }
    DbSet<Question> Questions { get; }
    DbSet<Option> Options { get; }
    DbSet<QuizAttempt> QuizAttempts { get; }
    DbSet<AttemptAnswer> AttemptAnswers { get; }
    DbSet<ReattemptRequest> ReattemptRequests { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
