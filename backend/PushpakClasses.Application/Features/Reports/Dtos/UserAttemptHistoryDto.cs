namespace PushpakClasses.Application.Features.Reports.Dtos;

public class UserAttemptHistoryDto
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public decimal TotalMarks { get; set; }
    public decimal Percent { get; set; }
    public bool Passed { get; set; }
    public int TabSwitchCount { get; set; }
}
