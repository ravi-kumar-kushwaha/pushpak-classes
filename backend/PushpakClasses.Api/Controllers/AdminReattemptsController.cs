using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Reattempts.Dtos;

namespace PushpakClasses.Api.Controllers;

[ApiController]
[Route("api/admin/reattempt-requests")]
[Authorize(Roles = "Admin")]
public class AdminReattemptsController : ControllerBase
{
    private readonly IQuizAttemptService _quizAttemptService;

    public AdminReattemptsController(IQuizAttemptService quizAttemptService)
    {
        _quizAttemptService = quizAttemptService;
    }

    /// <summary>
    /// Gets paged list of reattempt requests with optional status, quiz, and user filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReattemptRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReattemptRequests(
        [FromQuery] ReattemptRequestQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await _quizAttemptService.GetReattemptRequestsAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reviews a reattempt request (Approve or Reject), granting an extra attempt if approved and notifying the candidate.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ReattemptRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewReattemptRequest(
        Guid id,
        [FromBody] ReviewReattemptRequestDto request,
        [FromServices] IValidator<ReviewReattemptRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _quizAttemptService.ReviewReattemptRequestAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
