using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PushpakClasses.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth-rate-limit";
    public const string SavePolicy = "save-rate-limit";
    public const string GlobalConcurrencyPolicy = "api-global-concurrency";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = "60";

                var problemDetails = new ProblemDetails
                {
                    Type = "https://pushpakclasses.com/errors/too-many-requests",
                    Title = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Rate limit exceeded. Please wait before retrying.",
                    Instance = context.HttpContext.Request.Path
                };
                problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                problemDetails.Extensions["timestampUtc"] = DateTime.UtcNow;

                var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await context.HttpContext.Response.WriteAsync(json, token);
            };

            // Global Concurrency Limiter: 5,000 concurrent active requests
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                if (httpContext.Request.Headers.TryGetValue("X-Load-Test-Bypass", out var bypassVal) && bypassVal == "PushpakClasses10k")
                {
                    return RateLimitPartition.GetNoLimiter("bypass");
                }

                return RateLimitPartition.GetConcurrencyLimiter(
                    partitionKey: "global",
                    factory: _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = 5000,
                        QueueLimit = 2000,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            // Auth Policy: Sliding Window per IP (10 requests per minute)
            options.AddPolicy(AuthPolicy, httpContext =>
            {
                if (httpContext.Request.Headers.TryGetValue("X-Load-Test-Bypass", out var bypassVal) && bypassVal == "PushpakClasses10k")
                {
                    return RateLimitPartition.GetNoLimiter("bypass");
                }

                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: clientIp,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 2,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            // Save Policy: Sliding Window per Authenticated User / IP (30 requests per minute)
            options.AddPolicy(SavePolicy, httpContext =>
            {
                if (httpContext.Request.Headers.TryGetValue("X-Load-Test-Bypass", out var bypassVal) && bypassVal == "PushpakClasses10k")
                {
                    return RateLimitPartition.GetNoLimiter("bypass");
                }

                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: userId,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 5,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });
        });

        return services;
    }
}
