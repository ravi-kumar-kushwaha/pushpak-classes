using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Users.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Infrastructure.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IPushpakClassesDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasherService _passwordHasher;

    public AdminUserService(
        IPushpakClassesDbContext context,
        ICurrentUserService currentUserService,
        IPasswordHasherService passwordHasher)
    {
        _context = context;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(UserQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();

        // 1. Search by name or email
        if (!string.IsNullOrWhiteSpace(parameters.SearchQuery))
        {
            var search = parameters.SearchQuery.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(search) || u.Email.ToLower().Contains(search));
        }

        // 2. Filter by role
        if (parameters.Role.HasValue)
        {
            query = query.Where(u => u.Role == parameters.Role.Value);
        }

        // 3. Filter by active status
        if (parameters.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == parameters.IsActive.Value);
        }

        // Total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // 4. Sorting
        var isAscending = string.Equals(parameters.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = parameters.SortBy?.ToLowerInvariant() switch
        {
            "fullname" => isAscending ? query.OrderBy(u => u.FullName) : query.OrderByDescending(u => u.FullName),
            "email" => isAscending ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
            "role" => isAscending ? query.OrderBy(u => u.Role) : query.OrderByDescending(u => u.Role),
            _ => isAscending ? query.OrderBy(u => u.CreatedAtUtc) : query.OrderByDescending(u => u.CreatedAtUtc)
        };

        // 5. Pagination
        var items = await query
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(u => MapToUserDto(u))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserDto>(items, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<UserDto> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            throw new AuthException("User not found.", HttpStatusCode.NotFound);
        }

        return MapToUserDto(user);
    }

    public async Task<UserDto> CreateUserAsync(AdminCreateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (exists)
        {
            throw new AuthException("A user with this email address already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = request.PhoneNumber?.Trim(),
            Role = request.Role,
            IsActive = request.IsActive,
            IsEmailVerified = true,
            FailedLoginCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            IsDeleted = false
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _context.Users.AddAsync(user, cancellationToken);

        await WriteAuditLogAsync(
            action: "AdminCreateUser",
            entityId: user.Id,
            details: new { user.Email, user.Role, user.IsActive, user.FullName },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return MapToUserDto(user);
    }

    public async Task<UserDto> UpdateUserAsync(Guid id, AdminUpdateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            throw new AuthException("User not found.", HttpStatusCode.NotFound);
        }

        var oldState = new { user.FullName, user.PhoneNumber, user.ProfileImageUrl };

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();
        if (!string.IsNullOrEmpty(request.ProfileImageUrl))
        {
            user.ProfileImageUrl = request.ProfileImageUrl.Trim();
        }

        var newState = new { user.FullName, user.PhoneNumber, user.ProfileImageUrl };

        await WriteAuditLogAsync(
            action: "AdminUpdateUser",
            entityId: user.Id,
            details: new { Old = oldState, New = newState },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return MapToUserDto(user);
    }

    public async Task<AuthMessageResponseDto> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Admin must not be able to delete themselves
        var currentAdminId = _currentUserService.UserId;
        if (currentAdminId.HasValue && currentAdminId.Value == id)
        {
            throw new AuthException("Administrators cannot delete their own account.", HttpStatusCode.BadRequest);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            throw new AuthException("User not found.", HttpStatusCode.NotFound);
        }

        // Soft delete (Rule 6 of AGENT_RULES.md)
        user.IsDeleted = true;

        await WriteAuditLogAsync(
            action: "AdminDeleteUser",
            entityId: user.Id,
            details: new { user.Email, user.FullName, DeletedAtUtc = DateTime.UtcNow },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new AuthMessageResponseDto("User deleted successfully.");
    }

    public async Task<UserDto> UpdateStatusAsync(Guid id, UpdateUserStatusRequestDto request, CancellationToken cancellationToken = default)
    {
        // Admin must not be able to deactivate themselves
        var currentAdminId = _currentUserService.UserId;
        if (currentAdminId.HasValue && currentAdminId.Value == id && !request.IsActive)
        {
            throw new AuthException("Administrators cannot deactivate their own account.", HttpStatusCode.BadRequest);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            throw new AuthException("User not found.", HttpStatusCode.NotFound);
        }

        var previousStatus = user.IsActive;
        user.IsActive = request.IsActive;

        await WriteAuditLogAsync(
            action: "AdminUpdateUserStatus",
            entityId: user.Id,
            details: new { PreviousStatus = previousStatus, NewStatus = user.IsActive },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return MapToUserDto(user);
    }

    public async Task<UserDto> UpdateRoleAsync(Guid id, UpdateUserRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        // Admin must not be able to demote themselves
        var currentAdminId = _currentUserService.UserId;
        if (currentAdminId.HasValue && currentAdminId.Value == id && request.Role != UserRole.Admin)
        {
            throw new AuthException("Administrators cannot revoke their own administrator role.", HttpStatusCode.BadRequest);
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            throw new AuthException("User not found.", HttpStatusCode.NotFound);
        }

        var previousRole = user.Role;
        user.Role = request.Role;

        await WriteAuditLogAsync(
            action: "AdminUpdateUserRole",
            entityId: user.Id,
            details: new { PreviousRole = previousRole.ToString(), NewRole = user.Role.ToString() },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return MapToUserDto(user);
    }

    public async Task<AuthMessageResponseDto> ResetPasswordAsync(Guid id, AdminResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            throw new AuthException("User not found.", HttpStatusCode.NotFound);
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.TemporaryPassword);
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;

        // Revoke active refresh tokens to force re-login with temporary password
        var activeRefreshTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var rt in activeRefreshTokens)
        {
            rt.RevokedAtUtc = DateTime.UtcNow;
        }

        await WriteAuditLogAsync(
            action: "AdminResetPassword",
            entityId: user.Id,
            details: new { Message = "Admin reset password and revoked active refresh tokens." },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new AuthMessageResponseDto("Temporary password set successfully. User will need to log in with this new password.");
    }

    private async Task WriteAuditLogAsync(string action, Guid entityId, object details, CancellationToken cancellationToken)
    {
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUserService.UserId,
            Action = action,
            EntityName = nameof(User),
            EntityId = entityId.ToString(),
            DetailsJson = JsonSerializer.Serialize(details),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.AuditLogs.AddAsync(audit, cancellationToken);
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
