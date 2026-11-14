using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Application.Features.Users.Dtos;

public class AdminCreateUserRequestDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
}
