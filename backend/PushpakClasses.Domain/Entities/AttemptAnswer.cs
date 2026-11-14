namespace PushpakClasses.Domain.Entities;

public class AttemptAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AttemptId { get; set; }
    public QuizAttempt Attempt { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public string SelectedOptionIds { get; set; } = string.Empty;
    public bool IsCorrect { get; set; } = false;
    public decimal MarksAwarded { get; set; } = 0;
}
