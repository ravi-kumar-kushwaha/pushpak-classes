using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Domain.Entities;

public class EmailToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public EmailTokenType Type { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public bool IsUsed => UsedAtUtc != null;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsActive => !IsUsed && !IsExpired;
}
