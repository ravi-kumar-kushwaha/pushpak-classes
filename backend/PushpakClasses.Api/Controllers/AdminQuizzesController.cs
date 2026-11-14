using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Quizzes.Dtos;

namespace PushpakClasses.Api.Controllers;

[ApiController]
[Route("api/admin/quizzes")]
[Authorize(Roles = "Admin")]
public class AdminQuizzesController : ControllerBase
{
    private readonly IQuizService _quizService;

    public AdminQuizzesController(IQuizService quizService)
    {
        _quizService = quizService;
    }

    /// <summary>
    /// Gets paged list of quizzes with search, category/status filtering, and sorting.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<QuizDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetQuizzes([FromQuery] QuizQueryParameters parameters, CancellationToken cancellationToken)
    {
        var result = await _quizService.GetQuizzesAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets details of a single quiz by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuizById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _quizService.GetQuizByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new quiz.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateQuiz(
        [FromBody] CreateQuizRequestDto request,
        [FromServices] IValidator<CreateQuizRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _quizService.CreateQuizAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetQuizById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing quiz.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateQuiz(
        Guid id,
        [FromBody] UpdateQuizRequestDto request,
        [FromServices] IValidator<UpdateQuizRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _quizService.UpdateQuizAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Soft deletes a quiz.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteQuiz(Guid id, CancellationToken cancellationToken)
    {
        var result = await _quizService.DeleteQuizAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Publishes or unpublishes a quiz. A quiz must have at least 1 question to be published.
    /// </summary>
    [HttpPatch("{id:guid}/publish")]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPublishStatus(
        Guid id,
        [FromBody] PublishQuizRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _quizService.SetPublishStatusAsync(id, request.IsPublished, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Duplicates a quiz along with all its questions and options in an unpublished state.
    /// </summary>
    [HttpPost("{id:guid}/duplicate")]
    [ProducesResponseType(typeof(QuizDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DuplicateQuiz(Guid id, CancellationToken cancellationToken)
    {
        var result = await _quizService.DuplicateQuizAsync(id, cancellationToken);
        return CreatedAtAction(nameof(GetQuizById), new { id = result.Id }, result);
    }
}
