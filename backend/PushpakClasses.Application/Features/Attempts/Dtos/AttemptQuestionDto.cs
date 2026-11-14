namespace PushpakClasses.Application.Features.Attempts.Dtos;

public class AttemptQuestionDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string? TextHi { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Marks { get; set; }
    public int Order { get; set; }
    public List<AttemptOptionDto> Options { get; set; } = new();
}
