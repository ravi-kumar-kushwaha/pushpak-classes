using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Reports.Dtos;

namespace PushpakClasses.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public AdminReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Gets platform overview KPIs, user counts, quiz counts, attempts, and 30-day performance trends.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AdminDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminDashboard(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetAdminDashboardAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets paginated attempts across all quizzes and users with filtering by quiz, user, status, and date range.
    /// </summary>
    [HttpGet("attempts")]
    [ProducesResponseType(typeof(PagedResult<AdminAttemptDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminAttempts(
        [FromQuery] AdminAttemptQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetAdminAttemptsAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets detailed answers and review for an attempt (correct vs selected options).
    /// </summary>
    [HttpGet("attempts/{id:guid}")]
    [ProducesResponseType(typeof(AttemptReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdminAttemptById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetAttemptReviewAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets ranked leaderboard for a quiz. High scores ranked first, tie-breaks resolved by fastest completion time.
    /// </summary>
    [HttpGet("quizzes/{id:guid}/leaderboard")]
    [ProducesResponseType(typeof(QuizLeaderboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuizLeaderboard(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetQuizLeaderboardAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Exports formatted quiz results to an Excel spreadsheet (.xlsx) via ClosedXML.
    /// </summary>
    [HttpGet("quizzes/{id:guid}/results/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportQuizResults(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (fileBytes, fileName) = await _reportService.ExportQuizResultsToExcelAsync(id, cancellationToken);
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
