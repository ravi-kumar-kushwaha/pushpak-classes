using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Users.Dtos;

namespace PushpakClasses.Application.Common.Interfaces;

public interface IAdminUserService
{
    Task<PagedResult<UserDto>> GetUsersAsync(UserQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<UserDto> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto> CreateUserAsync(AdminCreateUserRequestDto request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateUserAsync(Guid id, AdminUpdateUserRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateStatusAsync(Guid id, UpdateUserStatusRequestDto request, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateRoleAsync(Guid id, UpdateUserRoleRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> ResetPasswordAsync(Guid id, AdminResetPasswordRequestDto request, CancellationToken cancellationToken = default);
}
