namespace PushpakClasses.Application.Features.Attempts.Dtos;

public class AttemptOptionDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string? TextHi { get; set; }
}
