namespace PushpakClasses.Application.Features.Reports.Dtos;

public class UserDashboardDto
{
    public int TotalAttempts { get; set; }
    public decimal AverageScore { get; set; }
    public decimal BestScore { get; set; }
    public decimal PassRate { get; set; }
    public int TotalQuizzesAttempted { get; set; }
    public List<UserAttemptHistoryDto> RecentAttempts { get; set; } = new();
}
