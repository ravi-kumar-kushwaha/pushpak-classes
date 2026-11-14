namespace PushpakClasses.Application.Common.Models;

public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string Provider { get; set; } = "Logging"; // "Logging" or "Smtp"
    public string FromEmail { get; set; } = "noreply@example.com";
    public string FromName { get; set; } = "PushpakClasses";
    public string SmtpHost { get; set; } = "smtp.example.com";
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? SmtpUserName { get; set; }
    public string? SmtpPassword { get; set; }
    public string AppBaseUrl { get; set; } = "http://localhost:4200";
}
