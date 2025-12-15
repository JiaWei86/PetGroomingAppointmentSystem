using System.Security.Cryptography;

namespace PetGroomingAppointmentSystem.Areas.Admin.Services;

/// <summary>
/// Password generation and hashing service implementation
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
    /// Hash password using BCrypt (placeholder for future implementation)
    /// TODO: Install BCrypt.Net-Next package and implement proper hashing
    /// </summary>
    public string HashPassword(string password)
    {
        // For now, return plain password
        // TODO: return BCrypt.Net.BCrypt.HashPassword(password);
    return password;
    }

    /// <summary>
    /// Verify password against hash (placeholder for future implementation)
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        // For now, simple comparison
        // TODO: return BCrypt.Net.BCrypt.Verify(password, hash);
        return password == hash;
    }
}
