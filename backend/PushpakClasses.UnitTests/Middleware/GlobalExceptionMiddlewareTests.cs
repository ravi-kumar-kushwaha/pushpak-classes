using System.Net;
using System.Text.Json;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PushpakClasses.Api.Middleware;
using PushpakClasses.Application.Common.Exceptions;
using Xunit;

namespace PushpakClasses.UnitTests.Middleware;

public class GlobalExceptionMiddlewareTests
{
    private readonly NullLogger<GlobalExceptionMiddleware> _logger = NullLogger<GlobalExceptionMiddleware>.Instance;

    [Fact]
    public async Task InvokeAsync_WhenAuthExceptionThrown_ShouldReturnProblemDetailsWithMatchingStatus()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "test-trace-id";

        var middleware = new GlobalExceptionMiddleware(
            next: (ctx) => throw new AuthException("Account is locked.", HttpStatusCode.Locked),
            logger: _logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Locked);
        context.Response.ContentType.Should().Contain("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseText = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.Locked);
        root.GetProperty("detail").GetString().Should().Be("Account is locked.");
        root.GetProperty("message").GetString().Should().Be("Account is locked.");
        root.GetProperty("traceId").GetString().Should().Be("test-trace-id");
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationExceptionThrown_ShouldReturnValidationProblemDetails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var validationFailures = new List<ValidationFailure>
        {
            new("Email", "Email is required."),
            new("Password", "Password is too short.")
        };

        var middleware = new GlobalExceptionMiddleware(
            next: (ctx) => throw new FluentValidation.ValidationException(validationFailures),
            logger: _logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Contain("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseText = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(400);
        root.GetProperty("title").GetString().Should().Contain("validation");
        root.TryGetProperty("errors", out var errorsProp).Should().BeTrue();
        errorsProp.TryGetProperty("Email", out var emailErrors).Should().BeTrue();
        emailErrors.EnumerateArray().First().GetString().Should().Be("Email is required.");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledExceptionThrown_ShouldReturn500InternalServerError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new GlobalExceptionMiddleware(
            next: (ctx) => throw new InvalidOperationException("Fatal database crash"),
            logger: _logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Contain("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseText = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(500);
        root.GetProperty("title").GetString().Should().Be("Internal Server Error");
    }
}
