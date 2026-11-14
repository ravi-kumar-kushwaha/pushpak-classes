namespace PushpakClasses.Application.Common.Models;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = "ReplaceWithYourSuperSecretJwtSigningKey32CharsMin!";
    public string Issuer { get; set; } = "PushpakClasses.Api";
    public string Audience { get; set; } = "PushpakClasses.Client";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
