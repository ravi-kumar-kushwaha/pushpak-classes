namespace PushpakClasses.Application.Features.Quizzes.Dtos;

public class QuestionDto
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string? TextHi { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Marks { get; set; }
    public int Order { get; set; }
    public List<OptionDto> Options { get; set; } = new();
}
