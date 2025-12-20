using PetGroomingAppointmentSystem.Models.ViewModels; // This using statement is already present in the latest version.

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

    /// <summary>
    /// Validate that experience years are realistic for the person's age derived from IC.
    /// </summary>
    /// <param name="experience">The years of experience.</param>
    /// <param name="ic">The IC number to derive age from.</param>
    ValidationResult ValidateExperienceAgainstAge(int experience, string ic);

    /// <summary>
    /// Validates a customer field for uniqueness and format.
    /// </summary>
    /// <param name="customerId">The ID of the customer to exclude (for edits).</param>
    /// <param name="fieldName">The name of the field to validate.</param>
    /// <param name="fieldValue">The value of the field to validate.</param>
    Task<ValidationResult> ValidateCustomerFieldAsync(string customerId, string fieldName, string fieldValue);
}