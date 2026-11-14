using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Attempts.Dtos;
using PushpakClasses.Application.Features.Reports.Dtos;

namespace PushpakClasses.Api.Controllers;

[ApiController]
[Route("api/attempts")]
[Authorize]
public class AttemptsController : ControllerBase
{
    private readonly IQuizAttemptService _quizAttemptService;
    private readonly IReportService _reportService;
    private readonly IPdfCertificateService _pdfCertificateService;

    public AttemptsController(
        IQuizAttemptService quizAttemptService,
        IReportService reportService,
        IPdfCertificateService pdfCertificateService)
    {
        _quizAttemptService = quizAttemptService;
        _reportService = reportService;
        _pdfCertificateService = pdfCertificateService;
    }

    /// <summary>
    /// Gets active attempt details, restoring question shuffle and Redis draft answers on page refresh.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ActiveAttemptDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveAttempt(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _quizAttemptService.GetActiveAttemptAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Saves draft answers to Redis. Rejects if server time > EndsAtUtc + 5s or attempt is already submitted.
    /// </summary>
    [HttpPost("{id:guid}/save")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(PushpakClasses.Api.Extensions.RateLimitingExtensions.SavePolicy)]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveDraftAnswers(
        Guid id,
        [FromBody] SaveAttemptAnswersRequestDto request,
        [FromServices] IValidator<SaveAttemptAnswersRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _quizAttemptService.SaveDraftAnswersAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Submits a quiz attempt (idempotent). Flushes answers from Redis to DB, marks Status Submitted, and enqueues scoring job.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(typeof(SubmitAttemptResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitQuizAttempt(
        Guid id,
        [FromBody] SaveAttemptAnswersRequestDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _quizAttemptService.SubmitQuizAttemptAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Records an anti-cheating tab switch event, incrementing TabSwitchCount.
    /// </summary>
    [HttpPost("{id:guid}/tab-switch")]
    [ProducesResponseType(typeof(TabSwitchResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordTabSwitch(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _quizAttemptService.RecordTabSwitchAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets the score, passing status, and outcome of a submitted quiz attempt.
    /// </summary>
    [HttpGet("{id:guid}/result")]
    [ProducesResponseType(typeof(AttemptResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttemptResult(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _quizAttemptService.GetAttemptResultAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reviews all questions, student selected options, and correct answers. Only available after submission.
    /// </summary>
    [HttpGet("{id:guid}/review")]
    [ProducesResponseType(typeof(AttemptReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttemptReview(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetAttemptReviewAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Downloads an official PDF Certificate of Achievement. Strictly allowed only if the quiz attempt was passed.
    /// </summary>
    [HttpGet("{id:guid}/certificate")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadCertificate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (fileBytes, fileName) = await _pdfCertificateService.GenerateCertificateAsync(id, cancellationToken);
        return File(fileBytes, "application/pdf", fileName);
    }
}
