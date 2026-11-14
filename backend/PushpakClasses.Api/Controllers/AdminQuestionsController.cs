using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Quizzes.Dtos;

namespace PushpakClasses.Api.Controllers;

[ApiController]
[Route("api/admin/quizzes/{quizId:guid}/questions")]
[Authorize(Roles = "Admin")]
public class AdminQuestionsController : ControllerBase
{
    private readonly IQuestionService _questionService;

    public AdminQuestionsController(IQuestionService questionService)
    {
        _questionService = questionService;
    }

    /// <summary>
    /// Gets all questions for a quiz in order.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<QuestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuestions(Guid quizId, CancellationToken cancellationToken)
    {
        var result = await _questionService.GetQuestionsByQuizIdAsync(quizId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets details of a single question within a quiz.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuestionById(Guid quizId, Guid id, CancellationToken cancellationToken)
    {
        var result = await _questionService.GetQuestionByIdAsync(quizId, id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Adds a new question with options to a quiz.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateQuestion(
        Guid quizId,
        [FromBody] CreateQuestionRequestDto request,
        [FromServices] IValidator<CreateQuestionRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _questionService.CreateQuestionAsync(quizId, request, cancellationToken);
        return CreatedAtAction(nameof(GetQuestionById), new { quizId = quizId, id = result.Id }, result);
    }

    /// <summary>
    /// Updates a question and its options.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateQuestion(
        Guid quizId,
        Guid id,
        [FromBody] UpdateQuestionRequestDto request,
        [FromServices] IValidator<UpdateQuestionRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _questionService.UpdateQuestionAsync(quizId, id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Soft deletes a question from a quiz.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteQuestion(Guid quizId, Guid id, CancellationToken cancellationToken)
    {
        var result = await _questionService.DeleteQuestionAsync(quizId, id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reorders questions within a quiz.
    /// </summary>
    [HttpPut("reorder")]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReorderQuestions(
        Guid quizId,
        [FromBody] ReorderQuestionsRequestDto request,
        [FromServices] IValidator<ReorderQuestionsRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _questionService.ReorderQuestionsAsync(quizId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Bulk imports questions with options from a CSV or Excel (.xlsx) file.
    /// </summary>
    [HttpPost("bulk-import")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BulkImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BulkImportQuestions(
        Guid quizId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            throw new AuthException("Please upload a valid CSV or Excel file.", HttpStatusCode.BadRequest);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".csv" && extension != ".xlsx")
        {
            throw new AuthException("Only CSV (.csv) and Excel (.xlsx) files are supported.", HttpStatusCode.BadRequest);
        }

        using var stream = file.OpenReadStream();
        var result = await _questionService.BulkImportQuestionsAsync(quizId, stream, file.FileName, cancellationToken);
        return Ok(result);
    }
}
