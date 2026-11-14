using PushpakClasses.Application.Features.Auth.Dtos;

namespace PushpakClasses.Application.Common.Interfaces;

public interface IAuthService
{
    Task<AuthMessageResponseDto> SignupAsync(SignupRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
}
