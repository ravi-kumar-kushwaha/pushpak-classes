using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IPushpakClassesDbContext _context;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IPushpakClassesDbContext context,
        IPasswordHasherService passwordHasher,
        IJwtTokenService jwtTokenService,
        IEmailService emailService,
        IOptions<EmailSettings> emailSettings,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task<AuthMessageResponseDto> SignupAsync(SignupRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Check if an active (non-soft-deleted) user already exists with this email
        var existingActiveUser = await _context.Users
            .AnyAsync(u => u.Email == normalizedEmail && !u.IsDeleted, cancellationToken);

        if (existingActiveUser)
        {
            throw new AuthException("An account with this email address already exists.", HttpStatusCode.Conflict);
        }

        // Check if a soft-deleted user exists with this email; if so, reactivate their record
        var softDeletedUser = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.IsDeleted, cancellationToken);

        User user;
        if (softDeletedUser != null)
        {
            user = softDeletedUser;
            user.FullName = request.FullName.Trim();
            user.PhoneNumber = request.PhoneNumber?.Trim();
            user.Role = UserRole.User; // Strictly enforced: self-signup is ALWAYS UserRole.User
            user.IsActive = true;
            user.IsEmailVerified = false;
            user.FailedLoginCount = 0;
            user.CreatedAtUtc = DateTime.UtcNow;
            user.IsDeleted = false;
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        }
        else
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                FullName = request.FullName.Trim(),
                Email = normalizedEmail,
                PhoneNumber = request.PhoneNumber?.Trim(),
                Role = UserRole.User, // Strictly enforced: self-signup is ALWAYS UserRole.User
                IsActive = true,
                IsEmailVerified = false,
                FailedLoginCount = 0,
                CreatedAtUtc = DateTime.UtcNow,
                IsDeleted = false
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            await _context.Users.AddAsync(user, cancellationToken);
        }

        var verifyToken = new EmailToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = GenerateSecureToken(),
            Type = EmailTokenType.Verify,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24),
            UsedAtUtc = null
        };
        await _context.EmailTokens.AddAsync(verifyToken, cancellationToken);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "UserSignup",
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            DetailsJson = $"{{\"Email\":\"{user.Email}\",\"Role\":\"{user.Role}\"}}",
            CreatedAtUtc = DateTime.UtcNow
        };
        await _context.AuditLogs.AddAsync(auditLog, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var verificationUrl = $"{_emailSettings.AppBaseUrl}/auth/verify-email?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(verifyToken.Token)}";
        _logger.LogInformation("\n=============================================================\nVERIFICATION LINK GENERATED FOR {Email}:\n{VerificationUrl}\n=============================================================", user.Email, verificationUrl);

        var emailBody = $@"
            <h2>Welcome to PushpakClasses, {user.FullName}!</h2>
            <p>Thank you for signing up. Please verify your email address by clicking the link below:</p>
            <p><a href='{verificationUrl}' style='display:inline-block;padding:10px 20px;background-color:#0d6efd;color:white;text-decoration:none;border-radius:5px;'>Verify Email Address</a></p>
            <p>Or use verification token: <strong>{verifyToken.Token}</strong></p>
            <p>Direct Link: <a href='{verificationUrl}'>{verificationUrl}</a></p>
            <p>This verification link will expire in 24 hours.</p>";

        await _emailService.SendEmailAsync(user.Email, "PushpakClasses - Verify Your Email", emailBody, cancellationToken);

        return new AuthMessageResponseDto("Registration successful! Please check your email to verify your account.");
    }

    public async Task<AuthMessageResponseDto> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default)
    {
        var token = request.Token?.Trim() ?? string.Empty;
        var query = _context.EmailTokens
            .Include(t => t.User)
            .Where(t => t.Token == token && t.Type == EmailTokenType.Verify);

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            query = query.Where(t => t.User.Email == normalizedEmail);
        }

        var tokenRecord = await query.FirstOrDefaultAsync(cancellationToken);

        if (tokenRecord == null || tokenRecord.IsUsed)
        {
            throw new AuthException("Invalid or already used verification token.");
        }

        if (DateTime.UtcNow > tokenRecord.ExpiresAtUtc)
        {
            throw new AuthException("Verification token has expired. Please request a new verification email.");
        }

        tokenRecord.UsedAtUtc = DateTime.UtcNow;
        tokenRecord.User.IsEmailVerified = true;

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = tokenRecord.UserId,
            Action = "EmailVerified",
            EntityName = nameof(User),
            EntityId = tokenRecord.UserId.ToString(),
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new AuthMessageResponseDto("Email verified successfully! You can now log in.");
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null)
        {
            throw new AuthException("Invalid email or password.", HttpStatusCode.Unauthorized);
        }

        // Check Lockout
        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
        {
            var remainingMinutes = Math.Ceiling((user.LockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
            throw new AuthException($"Account is temporarily locked out due to multiple failed login attempts. Please try again in {remainingMinutes} minutes.", (HttpStatusCode)423);
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(user, user.PasswordHash, request.Password);

        if (!isPasswordValid)
        {
            user.FailedLoginCount++;

            if (user.FailedLoginCount >= 5)
            {
                user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
                _logger.LogWarning("User {Email} locked out for 15 minutes due to 5 consecutive failed attempts.", user.Email);

                await _context.AuditLogs.AddAsync(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Action = "AccountLockedOut",
                    EntityName = nameof(User),
                    EntityId = user.Id.ToString(),
                    DetailsJson = $"{{\"FailedAttempts\":{user.FailedLoginCount},\"LockoutEndUtc\":\"{user.LockoutEndUtc}\"}}",
                    CreatedAtUtc = DateTime.UtcNow
                }, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
                throw new AuthException("Too many failed login attempts. Your account has been locked for 15 minutes.", (HttpStatusCode)423);
            }

            await _context.SaveChangesAsync(cancellationToken);
            throw new AuthException("Invalid email or password.", HttpStatusCode.Unauthorized);
        }

        if (!user.IsActive)
        {
            throw new AuthException("Your account is currently disabled. Please contact support.", HttpStatusCode.Forbidden);
        }

        // Reset lockout and failed login count on successful login
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;

        var (accessToken, accessExpires) = _jwtTokenService.GenerateAccessToken(user);
        var (refreshToken, refreshExpires) = _jwtTokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAtUtc = refreshExpires,
            RevokedAtUtc = null
        };

        await _context.RefreshTokens.AddAsync(refreshTokenEntity, cancellationToken);

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "UserLogin",
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAtUtc = accessExpires,
            User = MapToUserDto(user)
        };
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (tokenRecord == null || tokenRecord.RevokedAtUtc != null)
        {
            throw new AuthException("Invalid or revoked refresh token.", HttpStatusCode.Unauthorized);
        }

        if (DateTime.UtcNow >= tokenRecord.ExpiresAtUtc)
        {
            throw new AuthException("Refresh token has expired. Please login again.", HttpStatusCode.Unauthorized);
        }

        if (!tokenRecord.User.IsActive)
        {
            throw new AuthException("Account is currently disabled.", HttpStatusCode.Forbidden);
        }

        // Token rotation: Revoke current refresh token
        tokenRecord.RevokedAtUtc = DateTime.UtcNow;

        // Generate new pair
        var (newAccessToken, newAccessExpires) = _jwtTokenService.GenerateAccessToken(tokenRecord.User);
        var (newRefreshToken, newRefreshExpires) = _jwtTokenService.GenerateRefreshToken();

        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = tokenRecord.UserId,
            Token = newRefreshToken,
            ExpiresAtUtc = newRefreshExpires,
            RevokedAtUtc = null
        };

        await _context.RefreshTokens.AddAsync(newRefreshTokenEntity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            AccessTokenExpiresAtUtc = newAccessExpires,
            User = MapToUserDto(tokenRecord.User)
        };
    }

    public async Task<AuthMessageResponseDto> LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default)
    {
        var tokenRecord = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (tokenRecord != null && tokenRecord.RevokedAtUtc == null)
        {
            tokenRecord.RevokedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new AuthMessageResponseDto("Logged out successfully.");
    }

    public async Task<AuthMessageResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        // Anti-Enumeration: Always return the exact same generic message
        const string genericMessage = "If an account exists with this email address, password reset instructions have been sent.";

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return new AuthMessageResponseDto(genericMessage);
        }

        var resetToken = new EmailToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = GenerateSecureToken(),
            Type = EmailTokenType.ResetPassword,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30), // Exactly 30 minutes expiry
            UsedAtUtc = null
        };

        await _context.EmailTokens.AddAsync(resetToken, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var resetUrl = $"{_emailSettings.AppBaseUrl}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(resetToken.Token)}";
        var emailBody = $@"
            <h2>Password Reset Request</h2>
            <p>Hello {user.FullName},</p>
            <p>You requested a password reset for your PushpakClasses account. Click the button below to reset your password:</p>
            <p><a href='{resetUrl}' style='display:inline-block;padding:10px 20px;background-color:#dc3545;color:white;text-decoration:none;border-radius:5px;'>Reset Password</a></p>
            <p>Or use reset token: <strong>{resetToken.Token}</strong></p>
            <p>This link is valid for <strong>30 minutes</strong> only. If you did not request this, please ignore this email.</p>";

        await _emailService.SendEmailAsync(user.Email, "PushpakClasses - Password Reset Request", emailBody, cancellationToken);

        return new AuthMessageResponseDto(genericMessage);
    }

    public async Task<AuthMessageResponseDto> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var token = request.Token?.Trim() ?? string.Empty;
        var query = _context.EmailTokens
            .Include(t => t.User)
            .Where(t => t.Token == token && t.Type == EmailTokenType.ResetPassword);

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            query = query.Where(t => t.User.Email == normalizedEmail);
        }

        var tokenRecord = await query.FirstOrDefaultAsync(cancellationToken);

        if (tokenRecord == null || tokenRecord.IsUsed)
        {
            throw new AuthException("Invalid or already used password reset token.");
        }

        if (DateTime.UtcNow > tokenRecord.ExpiresAtUtc)
        {
            throw new AuthException("Password reset token has expired (valid for 30 minutes). Please request a new one.");
        }

        // Update password
        tokenRecord.User.PasswordHash = _passwordHasher.HashPassword(tokenRecord.User, request.NewPassword);
        tokenRecord.User.FailedLoginCount = 0;
        tokenRecord.User.LockoutEndUtc = null;
        tokenRecord.UsedAtUtc = DateTime.UtcNow;

        // Revoke all active refresh tokens for the user
        var activeRefreshTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == tokenRecord.UserId && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var rt in activeRefreshTokens)
        {
            rt.RevokedAtUtc = DateTime.UtcNow;
        }

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = tokenRecord.UserId,
            Action = "PasswordResetSuccess",
            EntityName = nameof(User),
            EntityId = tokenRecord.UserId.ToString(),
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new AuthMessageResponseDto("Password has been reset successfully. You can now log in with your new password.");
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        return Convert.ToHexString(randomBytes).ToLowerInvariant();
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
