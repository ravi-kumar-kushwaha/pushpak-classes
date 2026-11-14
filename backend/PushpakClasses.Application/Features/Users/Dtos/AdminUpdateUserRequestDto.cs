namespace PushpakClasses.Application.Features.Users.Dtos;

public class AdminUpdateUserRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
}
