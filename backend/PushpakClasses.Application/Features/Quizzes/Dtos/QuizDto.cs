namespace PushpakClasses.Application.Features.Quizzes.Dtos;

public class QuizDto
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
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
    public bool IsPublished { get; set; }
    public Guid CreatedByUserId { get; set; }
    public int QuestionCount { get; set; }
    public decimal TotalMarks { get; set; }
    public int AttemptCount { get; set; }
}
