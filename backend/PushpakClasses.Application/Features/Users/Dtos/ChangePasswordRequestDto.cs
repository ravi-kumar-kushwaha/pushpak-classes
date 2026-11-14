namespace PushpakClasses.Application.Features.Users.Dtos;

public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
    public string ConfirmPassword
    {
        get => ConfirmNewPassword;
        set => ConfirmNewPassword = value;
    }
}
