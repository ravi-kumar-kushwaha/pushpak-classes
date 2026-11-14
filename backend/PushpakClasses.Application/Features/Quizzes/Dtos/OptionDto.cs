namespace PushpakClasses.Application.Features.Quizzes.Dtos;

public class OptionDto
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string? TextHi { get; set; }
    public bool IsCorrect { get; set; }
}
