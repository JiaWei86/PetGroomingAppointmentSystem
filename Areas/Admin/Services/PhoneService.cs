using System.Text.RegularExpressions;

namespace PetGroomingAppointmentSystem.Areas.Admin.Services;

/// <summary>
/// Phone number formatting and validation service implementation
/// Supports Malaysian phone number format: 01X-XXXXXXX or 01X-XXXXXXXX
/// </summary>
public class PhoneService : IPhoneService
{
    private static readonly Regex PhoneFormatRegex = new(@"^01[0-9]-[0-9]{7,8}$", RegexOptions.Compiled);
    private static readonly Regex DigitsOnlyRegex = new(@"\D", RegexOptions.Compiled);

 /// <summary>
    /// Format phone number to 01X-XXXXXXX or 01X-XXXXXXXX format
    /// Accepts: 0121234567, 01212345678, 012-1234567, 012-12345678
    /// </summary>
    public string FormatPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
 return phoneNumber;

      // Remove all non-digits
        string cleaned = CleanPhoneNumber(phoneNumber);

   // Format as 01X-XXXXXXX or 01X-XXXXXXXX (3 digits, dash, 7-8 digits)
        if (cleaned.Length == 10)
 {
            // 0121234567 -> 012-1234567
            return $"{cleaned.Substring(0, 3)}-{cleaned.Substring(3)}";
        }
        else if (cleaned.Length == 11)
    {
            // 01212345678 -> 012-12345678
       return $"{cleaned.Substring(0, 3)}-{cleaned.Substring(3)}";
        }

     return cleaned;
    }

    /// <summary>
    /// Validate if phone number is in correct format: 01X-XXXXXXX or 01X-XXXXXXXX
/// </summary>
    public bool ValidatePhoneFormat(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
      return false;

    return PhoneFormatRegex.IsMatch(phoneNumber);
    }

    /// <summary>
    /// Clean phone number by removing all non-digits
    /// </summary>
    public string CleanPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber;

 return DigitsOnlyRegex.Replace(phoneNumber, "");
    }
}
