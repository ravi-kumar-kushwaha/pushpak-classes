namespace PushpakClasses.Application.Features.Users.Dtos;

public class ProfileImageResponseDto
{
    public string ProfileImageUrl { get; set; } = string.Empty;
    public string Message { get; set; } = "Profile image uploaded successfully.";
}
