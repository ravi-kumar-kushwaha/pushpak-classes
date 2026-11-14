using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Services;

public class PasswordHasherService : IPasswordHasherService
{
    private const int SaltByteSize = 16; // 128 bit
    private const int SubkeyByteSize = 32; // 256 bit
    private const int IterationCount = 10000;

    public string HashPassword(User user, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        byte[] salt = RandomNumberGenerator.GetBytes(SaltByteSize);
        byte[] subkey = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: IterationCount,
            numBytesRequested: SubkeyByteSize);

        byte[] outputBytes = new byte[1 + 4 + 4 + SaltByteSize + SubkeyByteSize];
        outputBytes[0] = 0x01; // Format marker
        WriteNetworkByteOrder(outputBytes, 1, (uint)KeyDerivationPrf.HMACSHA256);
        WriteNetworkByteOrder(outputBytes, 5, (uint)IterationCount);
        Buffer.BlockCopy(salt, 0, outputBytes, 9, SaltByteSize);
        Buffer.BlockCopy(subkey, 0, outputBytes, 9 + SaltByteSize, SubkeyByteSize);

        return Convert.ToBase64String(outputBytes);
    }

    public bool VerifyPassword(User user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(providedPassword))
            return false;

        byte[] decodedHashedPassword;
        try
        {
            decodedHashedPassword = Convert.FromBase64String(hashedPassword);
        }
        catch
        {
            return false;
        }

        if (decodedHashedPassword.Length != (1 + 4 + 4 + SaltByteSize + SubkeyByteSize) || decodedHashedPassword[0] != 0x01)
            return false;

        uint prf = ReadNetworkByteOrder(decodedHashedPassword, 1);
        int iterations = (int)ReadNetworkByteOrder(decodedHashedPassword, 5);

        byte[] salt = new byte[SaltByteSize];
        Buffer.BlockCopy(decodedHashedPassword, 9, salt, 0, SaltByteSize);

        byte[] expectedSubkey = new byte[SubkeyByteSize];
        Buffer.BlockCopy(decodedHashedPassword, 9 + SaltByteSize, expectedSubkey, 0, SubkeyByteSize);

        byte[] actualSubkey = KeyDerivation.Pbkdf2(
            password: providedPassword,
            salt: salt,
            prf: (KeyDerivationPrf)prf,
            iterationCount: iterations,
            numBytesRequested: SubkeyByteSize);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }

    private static void WriteNetworkByteOrder(byte[] buffer, int offset, uint value)
    {
        buffer[offset + 0] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)(value >> 0);
    }

    private static uint ReadNetworkByteOrder(byte[] buffer, int offset)
    {
        return ((uint)buffer[offset + 0] << 24)
             | ((uint)buffer[offset + 1] << 16)
             | ((uint)buffer[offset + 2] << 8)
             | ((uint)buffer[offset + 3]);
    }
}
