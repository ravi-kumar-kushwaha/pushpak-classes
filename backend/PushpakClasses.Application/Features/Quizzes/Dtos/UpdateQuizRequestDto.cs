namespace PushpakClasses.Application.Features.Quizzes.Dtos;

public class UpdateQuizRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public decimal PassingPercent { get; set; }
    public int MaxAttempts { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
}
