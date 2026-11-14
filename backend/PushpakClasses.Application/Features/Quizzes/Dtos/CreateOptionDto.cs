namespace PushpakClasses.Application.Features.Quizzes.Dtos;

public class CreateOptionDto
{
    public Guid? Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string? TextHi { get; set; }
    public bool IsCorrect { get; set; } = false;
}
