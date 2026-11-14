namespace PushpakClasses.Application.Features.Auth.Dtos;

public class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
