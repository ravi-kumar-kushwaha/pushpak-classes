using FluentAssertions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PushpakClasses.Api.Extensions;
using Xunit;

namespace PushpakClasses.UnitTests.Extensions;

public class RateLimitingTests
{
    [Fact]
    public void AddAppRateLimiting_ShouldRegisterPolicies()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAppRateLimiting();
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        // Assert
        options.Should().NotBeNull();
        options.RejectionStatusCode.Should().Be(429);
        options.GlobalLimiter.Should().NotBeNull();
    }
}
