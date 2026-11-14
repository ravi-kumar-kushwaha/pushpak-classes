using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Users.Dtos;

namespace PushpakClasses.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    /// <summary>
    /// Gets paged list of users with search, role/active filtering, and sorting.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers([FromQuery] UserQueryParameters parameters, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.GetUsersAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets single user details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.GetUserByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new user with a specified role.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser(
        [FromBody] AdminCreateUserRequestDto request,
        [FromServices] IValidator<AdminCreateUserRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _adminUserService.CreateUserAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUserById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing user's profile details.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(
        Guid id,
        [FromBody] AdminUpdateUserRequestDto request,
        [FromServices] IValidator<AdminUpdateUserRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _adminUserService.UpdateUserAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Soft deletes a user. Administrators cannot delete their own account.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.DeleteUserAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Activates or deactivates a user. Administrators cannot deactivate their own account.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequestDto request,
        [FromServices] IValidator<UpdateUserStatusRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _adminUserService.UpdateStatusAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates a user's role. Administrators cannot revoke their own admin role.
    /// </summary>
    [HttpPatch("{id:guid}/role")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRole(
        Guid id,
        [FromBody] UpdateUserRoleRequestDto request,
        [FromServices] IValidator<UpdateUserRoleRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _adminUserService.UpdateRoleAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Sets a temporary password for a user and revokes their active sessions.
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] AdminResetPasswordRequestDto request,
        [FromServices] IValidator<AdminResetPasswordRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _adminUserService.ResetPasswordAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
