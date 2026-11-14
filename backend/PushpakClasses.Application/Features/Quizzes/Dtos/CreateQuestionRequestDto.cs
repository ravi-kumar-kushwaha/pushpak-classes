using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Application.Features.Quizzes.Dtos;

public class CreateQuestionRequestDto
{
    public string Text { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string? TextHi { get; set; }
    public QuestionType Type { get; set; } = QuestionType.SingleChoice;
    public decimal Marks { get; set; } = 1;
    public int Order { get; set; } = 1;
    public List<CreateOptionDto> Options { get; set; } = new();
}
