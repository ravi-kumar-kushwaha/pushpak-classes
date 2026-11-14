using System.Text;
using System.Text.Json;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using PushpakClasses.Api;
using PushpakClasses.Api.Extensions;
using PushpakClasses.Api.HealthChecks;
using PushpakClasses.Api.Middleware;
using PushpakClasses.Application;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Infrastructure;
using PushpakClasses.Infrastructure.Persistence;
using Serilog;

// Load .env file if available in working or root directories
LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

// Map standard cross-platform flat environment variables into configuration if specified
MapEnvironmentVariables(builder.Configuration);

// Configure Serilog with structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "PushpakClasses.Api")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

// Configure CORS
var defaultOrigins = new[] { "http://localhost:4200", "https://localhost:4200", "http://localhost", "http://localhost:80", "http://localhost:5000", "http://localhost:5139" };
var configOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
var allowedOrigins = (configOrigins != null && configOrigins.Length > 0)
    ? defaultOrigins.Concat(configOrigins).Distinct().ToArray()
    : defaultOrigins;

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Rate Limiting (Hardened for 10,000 concurrent users)
builder.Services.AddAppRateLimiting();

// Add Health Checks for SQL Server and Redis
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("sql_server", tags: new[] { "ready", "db" })
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready", "cache" });

// Add Application (FluentValidation) & Infrastructure (EF Core with DbContextPool, Services)
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add Authentication & JWT Bearer
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Swagger with JWT Authorize Button
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "PushpakClasses API",
        Version = "v1",
        Description = "Backend API for PushpakClasses timed quiz platform targeting 10,000 concurrent users."
    });

    var securityScheme = new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "JWT Authentication",
        Description = "Enter your JWT Bearer token directly",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    };

    options.AddSecurityDefinition("Bearer", securityScheme);

    options.AddSecurityRequirement(document => new()
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// CLI Command: cleanup-users (Runs strictly in Development)
if (UserCleanupCli.IsRequested(args))
{
    await UserCleanupCli.ExecuteAsync(app, args);
    return;
}

// Forwarded headers for reverse proxies (Nginx load balancer)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// 1. Security Headers (Top of pipeline)
app.UseMiddleware<SecurityHeadersMiddleware>();

// 2. Structured request logging
app.UseSerilogRequestLogging();

// 3. Global Exception Handler returning RFC 7807 ProblemDetails
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("EnableSwagger", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PushpakClasses API v1");
        c.RoutePrefix = "swagger"; // Standard /swagger endpoint
    });
}

// 4. HTTPS, Static Files & CORS
if (!app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("EnableHttpsRedirection", false))
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
app.UseStaticFiles();

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}
var profilesUploadsPath = Path.Combine(uploadsPath, "profiles");
if (!Directory.Exists(profilesUploadsPath))
{
    Directory.CreateDirectory(profilesUploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseCors("AllowFrontend");

// 5. Rate Limiter
app.UseRateLimiter();

// 6. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 7. Health Checks Probes (/health, /health/ready, /health/live)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            service = "PushpakClasses API",
            timestampUtc = DateTime.UtcNow,
            components = report.Entries.Select(e => new
            {
                key = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
                error = e.Value.Exception?.Message
            })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// 8. Hangfire Dashboard & Recurring Jobs
try
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        DashboardTitle = "PushpakClasses Hangfire Dashboard",
        Authorization = new[] { new LocalOrAdminHangfireAuthorizationFilter() }
    });

    RecurringJob.AddOrUpdate<IAutoSubmitJob>(
        recurringJobId: "auto-submit-expired-quiz-attempts",
        methodCall: job => job.ProcessExpiredAttemptsAsync(CancellationToken.None),
        cronExpression: Cron.Minutely);
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to initialize Hangfire Dashboard/Recurring jobs. (Requires active persistence storage).");
}

// 9. Apply migrations and seed default Admin user on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<PushpakClassesDbContext>();
        if (context.Database.IsRelational())
        {
            logger.LogInformation("Checking and applying pending database migrations...");
            await context.Database.MigrateAsync();
        }

        var seeder = services.GetRequiredService<DbSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database migration/seed skipped at startup. (Start SQL Server container to complete migration/seeding).");
    }
}

try
{
    Log.Information("Starting PushpakClasses API (Hardened for 10,000 concurrent users)...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PushpakClasses API terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

public class LocalOrAdminHangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.Request.Host.Host == "localhost"
            || httpContext.Request.Host.Host == "127.0.0.1"
            || (httpContext.User.Identity?.IsAuthenticated == true && httpContext.User.IsInRole("Admin"));
    }
}

public partial class Program
{
    internal static void LoadDotEnv()
    {
        var searchDirs = new[]
        {
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), ".."),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..")
        };

        foreach (var dir in searchDirs)
        {
            var envPath = Path.Combine(dir, ".env");
            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;
                    var splitIdx = trimmed.IndexOf('=');
                    if (splitIdx > 0)
                    {
                        var key = trimmed.Substring(0, splitIdx).Trim();
                        var val = trimmed.Substring(splitIdx + 1).Trim().Trim('"', '\'');
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, val);
                        }
                    }
                }
                break;
            }
        }
    }

    internal static void MapEnvironmentVariables(ConfigurationManager configuration)
    {
        var conn = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(conn))
        {
            configuration["ConnectionStrings:DefaultConnection"] = conn;
        }

        var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
        if (!string.IsNullOrWhiteSpace(jwtSecret))
        {
            configuration["JwtSettings:Secret"] = jwtSecret;
        }

        var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            configuration["SeedData:AdminUser:Email"] = adminEmail;
        }

        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        if (!string.IsNullOrWhiteSpace(adminPassword))
        {
            configuration["SeedData:AdminUser:Password"] = adminPassword;
        }

        var redis = Environment.GetEnvironmentVariable("REDIS_CONNECTION");
        if (!string.IsNullOrWhiteSpace(redis))
        {
            configuration["RedisSettings:ConnectionString"] = redis;
        }
    }
}
