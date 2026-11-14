using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Domain.Entities;

public class QuizAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime EndsAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public QuizAttemptStatus Status { get; set; } = QuizAttemptStatus.InProgress;
    public decimal Score { get; set; } = 0;
    public decimal TotalMarks { get; set; } = 0;
    public decimal Percent { get; set; } = 0;
    public bool Passed { get; set; } = false;
    public string AnswersJson { get; set; } = string.Empty;
    public int TabSwitchCount { get; set; } = 0;

    // Navigation properties
    public ICollection<AttemptAnswer> AttemptAnswers { get; set; } = new List<AttemptAnswer>();
}
