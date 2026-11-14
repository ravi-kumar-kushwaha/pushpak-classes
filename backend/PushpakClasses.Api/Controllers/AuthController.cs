using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PushpakClasses.Api.Extensions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Features.Auth.Dtos;

namespace PushpakClasses.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// User self-registration. Role is strictly assigned as User. Sends verification email.
    /// </summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Signup(
        [FromBody] SignupRequestDto request,
        [FromServices] IValidator<SignupRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _authService.SignupAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Verifies email address using the token sent upon registration.
    /// </summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequestDto request,
        [FromServices] IValidator<VerifyEmailRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _authService.VerifyEmailAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Logs in user, returning a 15-minute JWT access token and 7-day refresh token.
    /// Implements 15-minute lockout after 5 consecutive failed attempts.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request,
        [FromServices] IValidator<LoginRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _authService.LoginAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Refreshes JWT access token using a valid, unrevoked refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequestDto request,
        [FromServices] IValidator<RefreshTokenRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _authService.RefreshTokenAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Revokes the specified refresh token to log out.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequestDto request,
        [FromServices] IValidator<LogoutRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _authService.LogoutAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Sends password reset email. Always returns generic success to prevent email enumeration.
    /// Reset token expires in 30 minutes.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequestDto request,
        [FromServices] IValidator<ForgotPasswordRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Resets user password using a valid 30-minute reset token.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequestDto request,
        [FromServices] IValidator<ResetPasswordRequestDto> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var result = await _authService.ResetPasswordAsync(request, cancellationToken);
        return Ok(result);
    }
}
