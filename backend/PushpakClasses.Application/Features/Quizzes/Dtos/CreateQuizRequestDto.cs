namespace PushpakClasses.Application.Features.Quizzes.Dtos;

public class CreateQuizRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public decimal PassingPercent { get; set; } = 50;
    public int MaxAttempts { get; set; } = 1;
    public bool ShuffleQuestions { get; set; } = false;
    public bool ShuffleOptions { get; set; } = false;
    public bool IsPublished { get; set; } = false;
}
