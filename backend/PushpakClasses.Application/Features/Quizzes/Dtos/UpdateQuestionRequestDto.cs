using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Application.Features.Quizzes.Dtos;

public class UpdateQuestionRequestDto
{
    public string Text { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string? TextHi { get; set; }
    public QuestionType Type { get; set; }
    public decimal Marks { get; set; }
    public int Order { get; set; }
    public List<CreateOptionDto> Options { get; set; } = new();
}
