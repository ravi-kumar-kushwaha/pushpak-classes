using PushpakClasses.Domain.Common;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Domain.Entities;

public class User : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; } = false;
    public int FailedLoginCount { get; set; } = 0;
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    // Navigation properties
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<EmailToken> EmailTokens { get; set; } = new List<EmailToken>();
    public ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<Quiz> CreatedQuizzes { get; set; } = new List<Quiz>();
    public ICollection<ReattemptRequest> ReattemptRequests { get; set; } = new List<ReattemptRequest>();
}
