namespace PushpakClasses.Application.Features.Reports.Dtos;

public class DailyAttemptStatDto
{
    public DateTime DateUtc { get; set; }
    public int AttemptCount { get; set; }
    public decimal AverageScore { get; set; }
}
