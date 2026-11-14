namespace PushpakClasses.Application.Features.Attempts.Dtos;

public class SubmitAttemptResponseDto
{
    public Guid AttemptId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAtUtc { get; set; }
    public string Message { get; set; } = string.Empty;
}
