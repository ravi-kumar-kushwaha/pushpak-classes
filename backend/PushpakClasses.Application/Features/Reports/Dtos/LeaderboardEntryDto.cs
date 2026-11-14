namespace PushpakClasses.Application.Features.Reports.Dtos;

public class LeaderboardEntryDto
{
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public decimal Score { get; set; }
    public decimal TotalMarks { get; set; }
    public decimal Percent { get; set; }
    public bool Passed { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public double DurationMinutesTaken { get; set; }
}
