namespace PushpakClasses.Application.Features.Attempts.Dtos;

public class StartQuizAttemptResponseDto
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public DateTime ServerTimeUtc { get; set; }
    public List<AttemptQuestionDto> Questions { get; set; } = new();
}
