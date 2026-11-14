namespace PushpakClasses.Application.Features.Attempts.Dtos;

public class TabSwitchResponseDto
{
    public Guid AttemptId { get; set; }
    public int TabSwitchCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
