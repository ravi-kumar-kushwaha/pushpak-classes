namespace PushpakClasses.Application.Common.Models;

public class RedisSettings
{
    public const string SectionName = "RedisSettings";

    public string ConnectionString { get; set; } = "localhost:6379";
    public bool Enabled { get; set; } = true;
    public int DefaultExpirationMinutes { get; set; } = 10;
}
