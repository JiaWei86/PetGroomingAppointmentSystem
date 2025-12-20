namespace PetGroomingAppointmentSystem.Services;

/// <summary>
/// Password generation and hashing service interface
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// Generate a random password with specified length
    /// </summary>
    string GenerateRandomPassword(int length = 12);

    /// <summary>
    /// Hash password using BCrypt (for future implementation)
    /// </summary>
    string HashPassword(string password);
    
    /// <summary>
    /// Verify password against hash (for future implementation)
    /// </summary>
    bool VerifyPassword(string password, string hash);
}
