namespace PushpakClasses.Application.Features.Attempts.Dtos;

public class ActiveAttemptDetailsDto
{
    public Guid AttemptId { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public DateTime ServerTimeUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TabSwitchCount { get; set; }
    public List<AttemptQuestionDto> Questions { get; set; } = new();
    public List<QuestionAnswerDto> DraftAnswers { get; set; } = new();
}
