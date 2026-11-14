using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Users.Dtos;

namespace PushpakClasses.Application.Common.Interfaces;

public interface IUserService
{
    Task<UserDto> GetCurrentProfileAsync(CancellationToken cancellationToken = default);
    Task<UserDto> UpdateCurrentProfileAsync(UpdateProfileRequestDto request, CancellationToken cancellationToken = default);
    Task<ProfileImageResponseDto> UploadProfilePhotoAsync(Stream fileStream, string fileName, string contentType, long fileLength, CancellationToken cancellationToken = default);
    Task RemoveProfilePhotoAsync(CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
}
