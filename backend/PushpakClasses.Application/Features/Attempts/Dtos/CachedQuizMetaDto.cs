namespace PushpakClasses.Application.Features.Attempts.Dtos;

public class CachedQuizMetaDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public int MaxAttempts { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
}
