namespace PushpakClasses.Application.Common.Models;

public class HangfireSettings
{
    public const string SectionName = "HangfireSettings";

    public bool UseInMemory { get; set; } = false;
}
