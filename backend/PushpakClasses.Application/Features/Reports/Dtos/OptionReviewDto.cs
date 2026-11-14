namespace PushpakClasses.Application.Features.Reports.Dtos;

public class OptionReviewDto
{
    public Guid OptionId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string? TextHi { get; set; }
    public bool IsSelectedByStudent { get; set; }
    public bool IsSelected => IsSelectedByStudent;
    public bool IsCorrect { get; set; }
}
