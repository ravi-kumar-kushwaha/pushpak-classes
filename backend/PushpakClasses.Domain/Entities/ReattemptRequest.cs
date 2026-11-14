using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Domain.Entities;

public class ReattemptRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
    public ReattemptRequestStatus Status { get; set; } = ReattemptRequestStatus.Pending;
    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
}
