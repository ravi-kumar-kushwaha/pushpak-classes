using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Infrastructure.Persistence;

namespace PushpakClasses.Api.Controllers;

[ApiController]
[Route("health/db")]
[Authorize(Roles = "Admin")]
public class DatabaseHealthController : ControllerBase
{
    private readonly PushpakClassesDbContext _context;
    private readonly IConfiguration _configuration;

    public DatabaseHealthController(PushpakClassesDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    /// <summary>
    /// Admin-only persistence and database health verification endpoint.
    /// Returns database provider, server host, database name, and record counts.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DatabaseHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDatabaseHealth(CancellationToken cancellationToken)
    {
        var rawConnStr = _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration["CONNECTION_STRING"]
            ?? string.Empty;

        string serverHost = "Unknown";
        string databaseName = "Unknown";
        try
        {
            var builder = new SqlConnectionStringBuilder(rawConnStr);
            serverHost = builder.DataSource;
            databaseName = builder.InitialCatalog;
        }
        catch { }

        var userCount = await _context.Users.IgnoreQueryFilters().CountAsync(cancellationToken);
        var quizCount = await _context.Quizzes.IgnoreQueryFilters().CountAsync(cancellationToken);
        var categoryCount = await _context.Categories.IgnoreQueryFilters().CountAsync(cancellationToken);
        var attemptCount = await _context.QuizAttempts.IgnoreQueryFilters().CountAsync(cancellationToken);

        return Ok(new DatabaseHealthResponse(
            Status: "Healthy",
            Provider: _context.Database.ProviderName,
            Server: serverHost,
            Database: databaseName,
            Counts: new DatabaseRecordCounts(
                Users: userCount,
                Quizzes: quizCount,
                Categories: categoryCount,
                Attempts: attemptCount
            ),
            TimestampUtc: DateTime.UtcNow
        ));
    }
}

public record DatabaseRecordCounts(int Users, int Quizzes, int Categories, int Attempts);

public record DatabaseHealthResponse(
    string Status,
    string? Provider,
    string Server,
    string Database,
    DatabaseRecordCounts Counts,
    DateTime TimestampUtc
);
