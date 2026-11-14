using PushpakClasses.Domain.Common;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Domain.Entities;

public class Question : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuizId { get; set; }
    public Quiz Quiz { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public string? TextHi { get; set; }
    public QuestionType Type { get; set; }
    public decimal Marks { get; set; }
    public int Order { get; set; }
    public bool IsDeleted { get; set; } = false;

    // Navigation properties
    public ICollection<Option> Options { get; set; } = new List<Option>();
}
