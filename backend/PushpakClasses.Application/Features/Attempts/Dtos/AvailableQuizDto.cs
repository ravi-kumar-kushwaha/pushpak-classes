namespace PushpakClasses.Application.Features.Attempts.Dtos;

public class AvailableQuizDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public decimal PassingPercent { get; set; }
    public int MaxAttempts { get; set; }
    public int AllowedAttempts { get; set; }
    public int AttemptsUsed { get; set; }
    public int AttemptsRemaining { get; set; }
    public int AttemptsLeft => AttemptsRemaining;
    public string Status { get; set; } = "Open";
    public string? ReattemptStatus { get; set; }
    public Guid? ReattemptRequestId { get; set; }
    public bool HasActiveAttempt { get; set; }
    public Guid? ActiveAttemptId { get; set; }
    public int QuestionCount { get; set; }
    public decimal TotalMarks { get; set; }
}
