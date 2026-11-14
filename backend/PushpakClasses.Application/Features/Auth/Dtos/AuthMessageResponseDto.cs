namespace PushpakClasses.Application.Features.Auth.Dtos;

public class AuthMessageResponseDto
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; } = true;

    public AuthMessageResponseDto() { }

    public AuthMessageResponseDto(string message, bool success = true)
    {
        Message = message;
        Success = success;
    }
}
