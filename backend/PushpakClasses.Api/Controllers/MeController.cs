using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Reports.Dtos;
using PushpakClasses.Application.Features.Users.Dtos;

namespace PushpakClasses.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IReportService _reportService;

    public MeController(IUserService userService, IReportService reportService)
    {
        _userService = userService;
        _reportService = reportService;
    }

    /// <summary>
    /// Gets current authenticated user's profile.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var result = await _userService.GetCurrentProfileAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates current authenticated user's profile (name and phone number).
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequestDto request,
        [FromServices] IValidator<UpdateProfileRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _userService.UpdateCurrentProfileAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Uploads profile image for current user (Max 2 MB, JPG and PNG only).
    /// </summary>
    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProfileImageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadPhoto(
        IFormFile? photo,
        CancellationToken cancellationToken)
    {
        if (photo == null || photo.Length == 0)
        {
            return BadRequest(new { message = "Photo file is required." });
        }

        using var stream = photo.OpenReadStream();
        var result = await _userService.UploadProfilePhotoAsync(
            stream,
            photo.FileName,
            photo.ContentType,
            photo.Length,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Removes profile image for current user.
    /// </summary>
    [HttpDelete("photo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemovePhoto(CancellationToken cancellationToken)
    {
        await _userService.RemoveProfilePhotoAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Changes password for current authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequestDto request,
        [FromServices] IValidator<ChangePasswordRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _userService.ChangePasswordAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets paged quiz attempt history for the current user.
    /// </summary>
    [HttpGet("attempts")]
    [ProducesResponseType(typeof(PagedResult<UserAttemptHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyAttempts(
        [FromQuery] QuizQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetUserAttemptsAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets dashboard summary metrics and recent quiz performance for the current user.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(UserDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyDashboard(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetUserDashboardAsync(cancellationToken);
        return Ok(result);
    }
}
