using PushpakClasses.Domain.Common;

namespace PushpakClasses.Domain.Entities;

public class Quiz : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int DurationMinutes { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public decimal PassingPercent { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public bool ShuffleQuestions { get; set; } = false;
    public bool ShuffleOptions { get; set; } = false;
    public bool IsPublished { get; set; } = false;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public bool IsDeleted { get; set; } = false;

    // Navigation properties
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
    public ICollection<ReattemptRequest> ReattemptRequests { get; set; } = new List<ReattemptRequest>();
}
