using FluentAssertions;
using Microsoft.Extensions.Configuration;
using PushpakClasses.Api;
using Xunit;

namespace PushpakClasses.UnitTests.Services;

public class UserCleanupCliTests
{
    [Theory]
    [InlineData("cleanup-users", true)]
    [InlineData("--cleanup-users", true)]
    [InlineData("CLEANUP-USERS", true)]
    [InlineData("--CLEANUP-USERS", true)]
    [InlineData("run", false)]
    [InlineData("--urls", false)]
    [InlineData("", false)]
    public void IsRequested_IdentifiesCliArgumentCorrectly(string arg, bool expected)
    {
        var result = UserCleanupCli.IsRequested(new[] { arg });
        result.Should().Be(expected);
    }

    [Fact]
    public void ProtectedAccounts_ReadsFromEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable("CLEANUP_PROTECTED_EMAILS", "admin@example.com,candidate@example.com");
        try
        {
            var protectedEmails = UserCleanupCli.GetProtectedEmails();
            protectedEmails.Should().Contain("admin@example.com");
            protectedEmails.Should().Contain("candidate@example.com");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLEANUP_PROTECTED_EMAILS", null);
        }
    }

    [Fact]
    public void ProtectedAccounts_ReadsFromConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "CleanupSettings:ProtectedEmails:0", "sysadmin@example.com" },
            { "SeedData:AdminUser:Email", "seededadmin@example.com" }
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var protectedEmails = UserCleanupCli.GetProtectedEmails(configuration);
        protectedEmails.Should().Contain("sysadmin@example.com");
        protectedEmails.Should().Contain("seededadmin@example.com");
    }

    [Fact]
    public void ProtectedAccounts_ReturnsSafeDefaultWhenUnset()
    {
        Environment.SetEnvironmentVariable("CLEANUP_PROTECTED_EMAILS", null);
        var protectedEmails = UserCleanupCli.GetProtectedEmails();
        protectedEmails.Should().NotBeEmpty();
        protectedEmails.Should().Contain("admin@example.com");
    }
}
