namespace PushpakClasses.Application.Features.Reports.Dtos;

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int TotalQuizzes { get; set; }
    public int TotalAttempts { get; set; }
    public decimal AverageScore { get; set; }
    public List<DailyAttemptStatDto> TrendLast30Days { get; set; } = new();
}
