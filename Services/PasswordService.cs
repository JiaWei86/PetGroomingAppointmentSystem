using System.Security.Cryptography;

namespace PetGroomingAppointmentSystem.Services;

/// <summary>
/// Password generation and hashing service implementation using PBKDF2
/// </summary>
public class PasswordService : IPasswordService
{
    private const string ValidChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%";

    /// <summary>
    /// Generate a random password with specified length
    /// </summary>
    public string GenerateRandomPassword(int length = 12)
    {
        if (length < 6 || length > 128)
            throw new ArgumentException("Password length must be between 6 and 128 characters.", nameof(length));

        var randomBytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = ValidChars[randomBytes[i] % ValidChars.Length];
        }

        return new string(chars);
    }

    /// <summary>
    /// Hash password using PBKDF2 with SHA256
    /// </summary>
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        // Generate a random salt (16 bytes)
        byte[] salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // Hash password using PBKDF2 with 10,000 iterations
        using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256))
        {
            byte[] hash = pbkdf2.GetBytes(32);

            // Combine salt + hash for storage (16 + 32 = 48 bytes)
            byte[] hashWithSalt = new byte[48];
            Buffer.BlockCopy(salt, 0, hashWithSalt, 0, 16);
            Buffer.BlockCopy(hash, 0, hashWithSalt, 16, 32);

            // Return as Base64 for storage in database
            return Convert.ToBase64String(hashWithSalt);
        }
    }

    /// <summary>
    /// Verify password against PBKDF2 hash
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
            return false;

        try
        {
            // Decode the hash from Base64
            byte[] hashWithSalt = Convert.FromBase64String(hash);

            // Extract salt (first 16 bytes)
            byte[] salt = new byte[16];
            Buffer.BlockCopy(hashWithSalt, 0, salt, 0, 16);

            // Hash the input password with the extracted salt
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256))
            {
                byte[] hash2 = pbkdf2.GetBytes(32);

                // Compare the computed hash with the stored hash
                for (int i = 0; i < 32; i++)
                {
                    if (hashWithSalt[i + 16] != hash2[i])
                        return false;
                }

                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}