using System.Net;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Users.Dtos;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IPushpakClassesDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IFileStorageService _fileStorageService;

    public UserService(
        IPushpakClassesDbContext context,
        ICurrentUserService currentUserService,
        IPasswordHasherService passwordHasher,
        IFileStorageService fileStorageService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
        _fileStorageService = fileStorageService;
    }

    public async Task<UserDto> GetCurrentProfileAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetAuthenticatedUserAsync(cancellationToken);
        return MapToUserDto(user);
    }

    public async Task<UserDto> UpdateCurrentProfileAsync(UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await GetAuthenticatedUserAsync(cancellationToken);

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        return MapToUserDto(user);
    }

    public async Task<ProfileImageResponseDto> UploadProfilePhotoAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long fileLength,
        CancellationToken cancellationToken = default)
    {
        var user = await GetAuthenticatedUserAsync(cancellationToken);
        var oldImageUrl = user.ProfileImageUrl;

        var imageUrl = await _fileStorageService.SaveProfileImageAsync(
            user.Id,
            fileStream,
            fileName,
            contentType,
            cancellationToken);

        user.ProfileImageUrl = imageUrl;
        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldImageUrl) && !string.Equals(oldImageUrl, imageUrl, StringComparison.OrdinalIgnoreCase))
        {
            _fileStorageService.DeleteFile(oldImageUrl);
        }

        return new ProfileImageResponseDto
        {
            ProfileImageUrl = imageUrl,
            Message = "Profile image uploaded successfully."
        };
    }

    public async Task RemoveProfilePhotoAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetAuthenticatedUserAsync(cancellationToken);
        var oldImageUrl = user.ProfileImageUrl;
        user.ProfileImageUrl = null;
        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldImageUrl))
        {
            _fileStorageService.DeleteFile(oldImageUrl);
        }
    }

    public async Task<AuthMessageResponseDto> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await GetAuthenticatedUserAsync(cancellationToken);

        var isCurrentPasswordValid = _passwordHasher.VerifyPassword(user, user.PasswordHash, request.CurrentPassword);
        if (!isCurrentPasswordValid)
        {
            throw new AuthException("Incorrect current password.", HttpStatusCode.BadRequest);
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "UserChangePassword",
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new AuthMessageResponseDto("Password changed successfully.");
    }

    private async Task<User> GetAuthenticatedUserAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            throw new AuthException("User is not authenticated.", HttpStatusCode.Unauthorized);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
        if (user == null)
        {
            throw new AuthException("User account not found.", HttpStatusCode.NotFound);
        }

        return user;
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            ProfileImageUrl = user.ProfileImageUrl,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }
}
