using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Quizzes.Dtos;

namespace PushpakClasses.Api.Controllers;

[ApiController]
[Route("api/admin/categories")]
[Authorize(Roles = "Admin")]
public class AdminCategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public AdminCategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// Gets paged list of categories with optional search, sorting, and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategories([FromQuery] CategoryQueryParameters parameters, CancellationToken cancellationToken)
    {
        var result = await _categoryService.GetCategoriesPagedAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a single category by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.GetCategoryByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new quiz category.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryRequestDto request,
        [FromServices] IValidator<CreateCategoryRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _categoryService.CreateCategoryAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCategoryById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing quiz category name.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryRequestDto request,
        [FromServices] IValidator<UpdateCategoryRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _categoryService.UpdateCategoryAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a quiz category if not in use by any quizzes.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.DeleteCategoryAsync(id, cancellationToken);
        return Ok(result);
    }
}
