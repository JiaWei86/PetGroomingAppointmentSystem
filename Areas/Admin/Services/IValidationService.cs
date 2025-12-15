namespace PetGroomingAppointmentSystem.Areas.Admin.Services;

/// <summary>
/// Validation service interface for common validation logic
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validate Malaysian IC format: xxxxxx-xx-xxxx
  /// </summary>
    bool ValidateICFormat(string ic);
    
    /// <summary>
    /// Validate name (only letters and spaces, 3-200 characters)
    /// </summary>
    bool ValidateName(string name);
    
    /// <summary>
    /// Validate email format
    /// </summary>
    bool ValidateEmail(string email);
    
    /// <summary>
    /// Validate experience year (0-50)
    /// </summary>
 bool ValidateExperienceYear(int? experienceYear);
    
    /// <summary>
    /// Validate position against allowed positions
    /// </summary>
    bool ValidatePosition(string position);
    
    /// <summary>
    /// Get list of valid positions
    /// </summary>
    string[] GetValidPositions();
}
