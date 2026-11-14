using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Application.Common.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(User user);
    (string Token, DateTime ExpiresAtUtc) GenerateRefreshToken();
}
