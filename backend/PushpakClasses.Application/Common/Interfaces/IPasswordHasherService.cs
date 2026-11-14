using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Application.Common.Interfaces;

public interface IPasswordHasherService
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string hashedPassword, string providedPassword);
}
