namespace PushpakClasses.Application.Features.Reattempts.Dtos;

public class ReattemptRequestDto
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedByUserName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
}
