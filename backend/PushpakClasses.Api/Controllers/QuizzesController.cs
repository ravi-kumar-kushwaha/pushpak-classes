using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Attempts.Dtos;
using PushpakClasses.Application.Features.Reattempts.Dtos;

namespace PushpakClasses.Api.Controllers;

[ApiController]
[Route("api/quizzes")]
[Authorize]
public class QuizzesController : ControllerBase
{
    private readonly IQuizAttemptService _quizAttemptService;

    public QuizzesController(IQuizAttemptService quizAttemptService)
    {
        _quizAttemptService = quizAttemptService;
    }

    /// <summary>
    /// Gets available published quizzes that are within active schedule and have remaining attempts for the current user.
    /// </summary>
    [HttpGet("available")]
    [ProducesResponseType(typeof(PagedResult<AvailableQuizDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAvailableQuizzes(
        [FromQuery] QuizQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await _quizAttemptService.GetAvailableQuizzesAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Starts a quiz attempt for the current user. Calculates server-authoritative timer, checks schedule and attempt limits, and returns sanitized questions.
    /// </summary>
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(StartQuizAttemptResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartQuizAttempt(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _quizAttemptService.StartQuizAttemptAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Requests a reattempt for a quiz whose allowed attempts have been exhausted.
    /// </summary>
    [HttpPost("{id:guid}/reattempt-request")]
    [ProducesResponseType(typeof(ReattemptRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestReattempt(
        Guid id,
        [FromBody] CreateReattemptRequestDto request,
        [FromServices] IValidator<CreateReattemptRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _quizAttemptService.CreateReattemptRequestAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
