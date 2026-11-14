namespace PushpakClasses.Application.Features.Users.Dtos;

public class UpdateProfileRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}
