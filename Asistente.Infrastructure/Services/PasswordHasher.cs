using System;
using System.Security.Cryptography;
using Asistente.Domain.Interfaces;

namespace Asistente.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));

        // Generate a 128-bit salt
        byte[] salt = new byte[128 / 8];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Derive a 256-bit subkey (using HMAC-SHA256 with 100,000 iterations)
        byte[] hashedBytes = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: 100000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 256 / 8);

        // Store salt and hash together
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hashedBytes)}";
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));
        if (storedHash == null) throw new ArgumentNullException(nameof(storedHash));

        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        try
        {
            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] hash = Convert.FromBase64String(parts[1]);

            byte[] testHash = Rfc2898DeriveBytes.Pbkdf2(
                password: password,
                salt: salt,
                iterations: 100000,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: 256 / 8);

            return CryptographicOperations.FixedTimeEquals(hash, testHash);
        }
        catch
        {
            return false;
        }
    }
}
