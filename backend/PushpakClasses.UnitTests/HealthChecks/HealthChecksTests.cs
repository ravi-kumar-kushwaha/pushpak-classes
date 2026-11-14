using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using PushpakClasses.Api.HealthChecks;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Infrastructure.Persistence;
using Xunit;

namespace PushpakClasses.UnitTests.HealthChecks;

public class HealthChecksTests
{
    [Fact]
    public async Task DatabaseHealthCheck_WhenCanConnect_ShouldReturnHealthy()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new PushpakClassesDbContext(options);
        var healthCheck = new DatabaseHealthCheck(context);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("responsive");
    }

    [Fact]
    public async Task RedisHealthCheck_WhenConnected_ShouldReturnHealthy()
    {
        // Arrange
        var redisMock = new Mock<IRedisService>();
        redisMock.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var healthCheck = new RedisHealthCheck(redisMock.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task RedisHealthCheck_WhenExceptionThrown_ShouldReturnDegraded()
    {
        // Arrange
        var redisMock = new Mock<IRedisService>();
        redisMock.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Redis connection timeout"));

        var healthCheck = new RedisHealthCheck(redisMock.Object);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("degraded");
    }
}
