using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PushpakClasses.Application.Common.Exceptions;

namespace PushpakClasses.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AuthException ex)
        {
            _logger.LogWarning(ex, "Business/Authentication exception: {Message} (Status: {StatusCode})", ex.Message, ex.StatusCode);
            await HandleProblemDetailsAsync(context, ex.StatusCode, ex.Message, "Authentication or Domain Error", "https://pushpakclasses.com/errors/auth");
        }
        catch (FluentValidation.ValidationException ex)
        {
            _logger.LogWarning("Validation failure: {Count} errors", ex.Errors.Count());
            await HandleValidationProblemDetailsAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled server exception occurred on {Path}", context.Request.Path);
            await HandleProblemDetailsAsync(context, HttpStatusCode.InternalServerError, "An unexpected server error occurred. Please try again later.", "Internal Server Error", "https://pushpakclasses.com/errors/server-error");
        }
    }

    private static async Task HandleProblemDetailsAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string detail,
        string title,
        string type)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problem = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = (int)statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };

        // Maintain backward compatibility for clients reading `message`
        problem.Extensions["message"] = detail;
        problem.Extensions["success"] = false;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["timestampUtc"] = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static async Task HandleValidationProblemDetailsAsync(
        HttpContext context,
        FluentValidation.ValidationException ex)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Type = "https://pushpakclasses.com/errors/validation",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Detail = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)),
            Instance = context.Request.Path
        };

        problem.Extensions["message"] = problem.Detail;
        problem.Extensions["success"] = false;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["timestampUtc"] = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
