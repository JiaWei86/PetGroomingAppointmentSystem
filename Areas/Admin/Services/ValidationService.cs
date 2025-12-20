using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PetGroomingAppointmentSystem.Models.ViewModels;

namespace PetGroomingAppointmentSystem.Areas.Admin.Services;

/// <summary>
/// Validation service implementation for common validation logic
/// </summary>
public class ValidationService : IValidationService
{
    private static readonly Regex ICFormatRegex = new(@"^\d{6}-\d{2}-\d{4}$", RegexOptions.Compiled);
    private static readonly Regex NameFormatRegex = new(@"^[a-zA-Z\s]+$", RegexOptions.Compiled);
    private static readonly Regex EmailFormatRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    
    private static readonly string[] ValidPositions = new[]
    {
        "Senior Groomer",
        "Junior Groomer",
        "Groomer Assistant"
    };

    /// <summary>
    /// Validate Malaysian IC format: xxxxxx-xx-xxxx
    /// First 6 digits must be a valid date (YYMMDD)
    /// Must be 18-60 years old
    /// </summary>
    public bool ValidateICFormat(string ic)
    {
    if (string.IsNullOrWhiteSpace(ic))
   return false;

        // Check basic format
      if (!ICFormatRegex.IsMatch(ic))
            return false;

        // Extract date part (first 6 digits before the dash)
        string datePart = ic.Substring(0, 6);
        
        // Parse year, month, day
     if (!int.TryParse(datePart.Substring(0, 2), out int year))
 return false;
     if (!int.TryParse(datePart.Substring(2, 2), out int month))
    return false;
        if (!int.TryParse(datePart.Substring(4, 2), out int day))
 return false;

        // Validate month (01-12)
     if (month < 1 || month > 12)
   return false;

        // Days in each month (including leap year February)
        int[] daysInMonth = { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        if (day < 1 || day > daysInMonth[month - 1])
     return false;

        // Determine full year (assume 1900s for year >= 50, 2000s for year < 50)
        int fullYear = year >= 50 ? 1900 + year : 2000 + year;

        // Additional validation for February in non-leap years
        if (month == 2 && day == 29)
      {
     // Check if leap year
         bool isLeapYear = (fullYear % 4 == 0 && fullYear % 100 != 0) || (fullYear % 400 == 0);
       
  if (!isLeapYear)
 return false;
        }

        // Validate that the date is not in the future
        int currentFullYear = DateTime.Now.Year;
        int currentMonth = DateTime.Now.Month;
  int currentDay = DateTime.Now.Day;
   
      if (fullYear > currentFullYear)
  return false;
        
        if (fullYear == currentFullYear && month > currentMonth)
      return false;

        if (fullYear == currentFullYear && month == currentMonth && day > currentDay)
            return false;

        // ========== Validate age (must be 18-60 years old) ==========
 DateTime birthDate = new DateTime(fullYear, month, day);
    DateTime today = DateTime.Now;
        
      // Calculate age
 int age = today.Year - birthDate.Year;
        
        // Adjust if birthday hasn't occurred yet this year
        if (today.Month < birthDate.Month || 
  (today.Month == birthDate.Month && today.Day < birthDate.Day))
        {
    age--;
    }
        
      // Must be between 18 and 60 (inclusive)
        if (age < 18 || age > 60)
     return false;

 return true;
    }

    /// <summary>
    /// Validate name (only letters and spaces, 3-200 characters)
    /// </summary>
    public bool ValidateName(string name)
    {
   if (string.IsNullOrWhiteSpace(name))
        return false;

   if (name.Length < 3 || name.Length > 200)
       return false;

   return NameFormatRegex.IsMatch(name);
    }

    /// <summary>
    /// Validate email format
    /// </summary>
    public bool ValidateEmail(string email)
    {
     if (string.IsNullOrWhiteSpace(email))
     return false;

 if (email.Length > 150)
   return false;

    return EmailFormatRegex.IsMatch(email);
    }

    /// <summary>
    /// Validate experience year (0-50)
    /// </summary>
    public bool ValidateExperienceYear(int? experienceYear)
    {
    if (!experienceYear.HasValue)
return true; // Optional field

    return experienceYear.Value >= 0 && experienceYear.Value <= 50;
    }

    /// <summary>
    /// Validate position against allowed positions
    /// </summary>
    public bool ValidatePosition(string position)
    {
        if (string.IsNullOrWhiteSpace(position))
            return false;

        return ValidPositions.Contains(position);
    }

    /// <summary>
    /// Get list of valid positions
    /// </summary>
    public string[] GetValidPositions()
    {
        return ValidPositions;
    }

    /// <summary>
    /// Validate that experience years are realistic for the person's age derived from IC.
    /// The experience cannot be more than (Age - 18).
    /// </summary>
    public ValidationResult ValidateExperienceAgainstAge(int experience, string ic)
    {
        if (string.IsNullOrWhiteSpace(ic) || !ICFormatRegex.IsMatch(ic))
 {
 // Don't validate if IC is invalid, as another validator will catch that.
 return new ValidationResult { IsValid = true };
 }

        try
        {
            string datePart = ic.Substring(0, 6);
            if (!int.TryParse(datePart.Substring(0, 2), out int year)) return new ValidationResult { IsValid = true };
            if (!int.TryParse(datePart.Substring(2, 2), out int month)) return new ValidationResult { IsValid = true };
            if (!int.TryParse(datePart.Substring(4, 2), out int day)) return new ValidationResult { IsValid = true };

            int fullYear = year >= 50 ? 1900 + year : 2000 + year;

            DateTime birthDate = new DateTime(fullYear, month, day);
            DateTime today = DateTime.Now;
            int age = today.Year - birthDate.Year;

            if (today.Month < birthDate.Month || (today.Month == birthDate.Month && today.Day < birthDate.Day))
            {
                age--;
            }

            // Experience cannot be greater than the number of years they could have been working (age - 18)
            if (experience > (age - 18))
            {
                return new ValidationResult
                {
                    IsValid = false, // Assuming ValidationResult has an IsValid property
                    ErrorMessage = $"Experience of {experience} years is unrealistic for someone aged {age}."
                };
            }

 return new ValidationResult { IsValid = true };
        }
        catch
        {
 return new ValidationResult { IsValid = true }; // Fail silently if IC parsing has an issue
        }
    }

    /// <summary>
    /// Validates a customer field for uniqueness and format.
    /// This method is intended to be implemented by a service that has access to the database,
    /// as ValidationService itself does not have DB context.
    /// </summary>
    public Task<ValidationResult> ValidateCustomerFieldAsync(string customerId, string fieldName, string fieldValue)
    {
        throw new NotImplementedException("ValidateCustomerFieldAsync is not implemented in ValidationService. It should be handled by a service with database access.");
    }
}
