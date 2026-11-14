namespace PushpakClasses.Application.Features.Reports.Dtos;

public class QuizLeaderboardDto
{
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = string.Empty;
    public int TotalParticipants { get; set; }
    public List<LeaderboardEntryDto> Leaderboard { get; set; } = new();
}
