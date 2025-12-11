using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace PetGroomingAppointmentSystem.Areas.Customer.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Phone number cannot be empty")]
        [StringLength(20, MinimumLength = 11, ErrorMessage = "Phone number must be in format 01X-XXXXXXXX or 01X-XXXXXXXXX")]
        [RegularExpression(@"^01[0-9]-?[0-9]{7,8}$", ErrorMessage = "Phone number must start with 01X and contain 8-9 digits (e.g., 0121234567 or 012-1234567)")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Full name cannot be empty")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Name must be between 3-200 characters")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Name must contain only letters and spaces")]
        public string Name { get; set; }

        [Required(ErrorMessage = "IC number cannot be empty")]
        [StringLength(20, MinimumLength = 14, ErrorMessage = "IC number must be in format xxxxxx-xx-xxxx")]
        [RegularExpression(@"^\d{6}-\d{2}-\d{4}$", ErrorMessage = "IC number must be in format xxxxxx-xx-xxxx (e.g., 123456-78-9012)")]
        [ValidMalaysianIC]
        public string IC { get; set; }

        [Required(ErrorMessage = "Email cannot be empty")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(150, ErrorMessage = "Email must not exceed 150 characters")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password cannot be empty")]
        [StringLength(200, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]).{8,}$",
            ErrorMessage = "Password must contain at least 8 characters, including 1 uppercase letter, 1 lowercase letter, and 1 symbol")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm password cannot be empty")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }
    }

    /// <summary>
    /// Custom validation attribute for Malaysian IC number
    /// Format: YYMMDD-SS-GGGG
    /// YYMMDD: Birth date (Year-Month-Day) - YY must not be more than current year and not older than 100 years
    /// SS: State code (01-16)
    /// GGGG: Sequential number
    /// </summary>
    public class ValidMalaysianICAttribute : ValidationAttribute
    {
        private static readonly Dictionary<string, string> MalaysianStates = new()
        {
            { "01", "Johor" },
            { "02", "Kedah" },
            { "03", "Kelantan" },
            { "04", "Malacca" },
            { "05", "Negeri Sembilan" },
            { "06", "Pahang" },
            { "07", "Penang" },
            { "08", "Perak" },
            { "09", "Perlis" },
            { "10", "Selangor" },
            { "11", "Terengganu" },
            { "12", "Sabah" },
            { "13", "Sarawak" },
            { "14", "Kuala Lumpur" },
            { "15", "Labuan" },
            { "16", "Putrajaya" }
        };

        // Days in each month (non-leap year)
        private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        public override string FormatErrorMessage(string name)
        {
            return "IC number is invalid. Please check the birth date (YYMMDD) and state code (01-16).";
        }

        public override bool IsValid(object value)
        {
            if (value is not string ic || string.IsNullOrEmpty(ic))
                return true; // Let [Required] handle empty values

            // Remove dashes to get just the digits
            string icDigits = ic.Replace("-", "");

            if (icDigits.Length != 12)
                return false;

            // Extract parts
            string yearPart = icDigits.Substring(0, 2);   // YY
            string monthPart = icDigits.Substring(2, 2);  // MM
            string dayPart = icDigits.Substring(4, 2);    // DD
            string statePart = icDigits.Substring(6, 2);  // SS

            // Parse year
            if (!int.TryParse(yearPart, out int year) || year < 0 || year > 99)
                return false;

            // Convert 2-digit year to 4-digit year
            int fullYear = year <= int.Parse(DateTime.Now.Year.ToString().Substring(2))
                ? 2000 + year
                : 1900 + year;

            // Check if year is not more than 100 years old
            int currentYear = DateTime.Now.Year;
            int age = currentYear - fullYear;
            if (age < 0 || age > 100)
                return false;

            // Parse month
            if (!int.TryParse(monthPart, out int month) || month < 1 || month > 12)
                return false;

            // Parse day
            if (!int.TryParse(dayPart, out int day) || day < 1)
                return false;

            // Check if it's a leap year
            bool isLeapYear = IsLeapYear(fullYear);

            // Get max days for the month
            int maxDays = DaysInMonth[month - 1];
            if (month == 2 && isLeapYear)
                maxDays = 29;

            // Validate day against max days in month
            if (day > maxDays)
                return false;

            // Validate state code (01-16)
            if (!MalaysianStates.ContainsKey(statePart))
                return false;

            return true;
        }

        private static bool IsLeapYear(int year)
        {
            return (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
        }
    }
}
