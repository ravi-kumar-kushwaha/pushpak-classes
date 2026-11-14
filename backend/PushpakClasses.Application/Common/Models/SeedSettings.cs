namespace PushpakClasses.Application.Common.Models;

public class SeedSettings
{
    public const string SectionName = "SeedData";

    public AdminUserSeed AdminUser { get; set; } = new();
}

public class AdminUserSeed
{
    public string Email { get; set; } = "admin@example.com";
    public string Password { get; set; } = "ChangeMe123!";
    public string FullName { get; set; } = "PushpakClasses Administrator";
    public string? PhoneNumber { get; set; } = "+15555555555";
}
