using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PushpakClasses.Infrastructure;
using Xunit;

namespace PushpakClasses.UnitTests.Persistence;

public class FailFastDatabaseConfigurationTests
{
    [Fact]
    public void AddInfrastructureServices_WhenConnectionStringMissing_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        var act = () => services.AddInfrastructureServices(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'ConnectionStrings__DefaultConnection'*missing*");
    }

    [Theory]
    [InlineData("Data Source=pushpakclasses.db")]
    [InlineData("Filename=test.db")]
    [InlineData("Data Source=mydata.sqlite;Version=3;")]
    public void AddInfrastructureServices_WhenSqliteOrNonSqlServerConfigured_ThrowsInvalidOperationException(string connStr)
    {
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:DefaultConnection", connStr }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        var act = () => services.AddInfrastructureServices(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Non-persistent/SQLite database detected*");
    }

    [Fact]
    public void AddInfrastructureServices_WhenMalformedConnectionString_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:DefaultConnection", "Invalid;Malformed===ConnStr;" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        var act = () => services.AddInfrastructureServices(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a valid Microsoft SQL Server connection string*");
    }

    [Fact]
    public void AddInfrastructureServices_WhenValidSqlServerConnectionString_Succeeds()
    {
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:DefaultConnection", "Server=testserver,1433;Database=TestDb;User Id=sa;Password=Secret123!;TrustServerCertificate=True;" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        var act = () => services.AddInfrastructureServices(configuration);

        act.Should().NotThrow();
    }
}
