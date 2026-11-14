namespace PushpakClasses.Application.Features.Reports.Dtos;

public class QuestionReviewDto
{
    public Guid QuestionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string? TextHi { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Marks { get; set; }
    public decimal MarksAwarded { get; set; }
    public bool IsCorrect { get; set; }
    public List<OptionReviewDto> Options { get; set; } = new();
}
