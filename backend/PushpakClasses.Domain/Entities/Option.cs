namespace PushpakClasses.Domain.Entities;

public class Option
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public string? TextHi { get; set; }
    public bool IsCorrect { get; set; } = false;
}
