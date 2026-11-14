namespace PushpakClasses.Application.Features.Auth.Dtos;

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public UserDto User { get; set; } = new();
}
