using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
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
        [ValidName]
        public string Name { get; set; }

        [Required(ErrorMessage = "IC number cannot be empty")]
        [StringLength(20, MinimumLength = 14, ErrorMessage = "IC number must be in format xxxxxx-xx-xxxx")]
        [RegularExpression(@"^\d{6}-\d{2}-\d{4}$", ErrorMessage = "IC number must be in format xxxxxx-xx-xxxx (e.g., 123456-78-9012)")]
        [ValidMalaysianIC]
        public string IC { get; set; }

        [Required(ErrorMessage = "Email cannot be empty")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(150, ErrorMessage = "Email must not exceed 150 characters")]
        [ValidEmail]
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
    /// YYMMDD: Birth date (Year-Month-Day)
    /// SS: State code (01-16)
    /// GGGG: Sequential number
    /// </summary>
    public class ValidMalaysianICAttribute : ValidationAttribute
    {
        private static readonly Regex ICFormatRegex = new(@"^\d{6}-\d{2}-\d{4}$", RegexOptions.Compiled);

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

        // Days in each month (including leap year February)
        private static readonly int[] DaysInMonthLeap = { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        public override string FormatErrorMessage(string name)
        {
            return "IC number is invalid. Please check the birth date (YYMMDD) and state code (01-16).";
        }

        public override bool IsValid(object value)
        {
            if (value is not string ic || string.IsNullOrEmpty(ic))
                return true; // Let [Required] handle empty values

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

            // Validate day against max days in month
            if (day < 1 || day > DaysInMonthLeap[month - 1])
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
            
            // ✅ 修复：州码应该从位置 7 开始（跳过日期部分和第一个连字符）
            string statePart = ic.Substring(7, 2);
            if (!MalaysianStates.ContainsKey(statePart))
                return false;

            return true;
        }

        private static bool IsLeapYear(int year)
        {
            return (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
        }
    }

    /// <summary>
    /// Custom validation attribute for name
    /// </summary>
    public class ValidNameAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value == null)
                return true;

            string name = value.ToString().Trim();
            return !string.IsNullOrEmpty(name);
        }
    }

    /// <summary>
    /// Custom validation attribute for email
    /// </summary>
    public class ValidEmailAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value == null)
                return true;

            string email = value.ToString().Trim();
            return !string.IsNullOrEmpty(email);
        }
    }
}
