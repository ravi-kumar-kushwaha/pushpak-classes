namespace PushpakClasses.Application.Features.Attempts.Dtos;

public class AttemptResultDto
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal TotalMarks { get; set; }
    public decimal Percent { get; set; }
    public bool Passed { get; set; }
    public int TabSwitchCount { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
}
