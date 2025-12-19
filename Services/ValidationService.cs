using System;
using System.Text.RegularExpressions;
using PetGroomingAppointmentSystem.Models;

namespace PetGroomingAppointmentSystem.Services
{
    /// <summary>
    /// Service for field validation operations
    /// </summary>
    public class ValidationService : IValidationService
    {
        private readonly DB _dbContext;
        private readonly IPhoneService _phoneService;

        public ValidationService(DB dbContext, IPhoneService phoneService)
        {
            _dbContext = dbContext;
            _phoneService = phoneService;
        }

        /// <summary>
        /// Validates name format and constraints
        /// </summary>
        public (bool isValid, string errorMessage) ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return (false, "Name cannot be empty.");

            name = name.Trim();

            if (name.Length < 3 || name.Length > 200)
                return (false, "Name must be between 3-200 characters.");

            if (!Regex.IsMatch(name, @"^[a-zA-Z\s]+$"))
                return (false, "Name must contain only letters and spaces.");

            return (true, "Valid name.");
        }

        /// <summary>
        /// Validates email format and checks if already registered
        /// </summary>
        public (bool isValid, string errorMessage) ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return (false, "Email cannot be empty.");

            email = email.Trim();

            if (email.Length > 150)
                return (false, "Email must not exceed 150 characters.");

            if (!Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                return (false, "Please enter a valid email address.");

            if (_dbContext.Users.Any(u => u.Email == email))
                return (false, "Email already registered.");

            return (true, "Valid email.");
        }

        /// <summary>
        /// Validates Malaysian IC number format and content
        /// </summary>
        public (bool isValid, string errorMessage) ValidateIC(string ic)
        {
            if (string.IsNullOrWhiteSpace(ic))
                return (false, "IC number cannot be empty.");

            ic = ic.Trim();

            if (!Regex.IsMatch(ic, @"^\d{6}-\d{2}-\d{4}$"))
                return (false, "IC number must be in format xxxxxx-xx-xxxx.");

            // Extract date part
            string datePart = ic.Substring(0, 6);
            if (!int.TryParse(datePart.Substring(0, 2), out int year))
                return (false, "Invalid year in IC.");
            if (!int.TryParse(datePart.Substring(2, 2), out int month))
                return (false, "Invalid month in IC.");
            if (!int.TryParse(datePart.Substring(4, 2), out int day))
                return (false, "Invalid day in IC.");

            // Validate month
            if (month < 1 || month > 12)
                return (false, "Invalid month in IC (must be 01-12).");

            // Days in month
            int[] daysInMonth = { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            if (day < 1 || day > daysInMonth[month - 1])
                return (false, "Invalid day in IC for the given month.");

            // Convert year
            int fullYear = year >= 50 ? 1900 + year : 2000 + year;

            // Check leap year for Feb 29
            if (month == 2 && day == 29)
            {
                bool isLeapYear = (fullYear % 4 == 0 && fullYear % 100 != 0) || (fullYear % 400 == 0);
                if (!isLeapYear)
                    return (false, "Invalid leap year date in IC.");
            }

            // Check date not in future
            int currentYear = DateTime.Now.Year;
            int currentMonth = DateTime.Now.Month;
            int currentDay = DateTime.Now.Day;

            if (fullYear > currentYear || (fullYear == currentYear && month > currentMonth) || (fullYear == currentYear && month == currentMonth && day > currentDay))
                return (false, "IC date cannot be in the future.");

            // ✅ 修复：州码应该从位置 7 开始（跳过日期部分和第一个连字符）
            string stateCode = ic.Substring(7, 2);
            var validStates = new[] { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15", "16" };
            if (!validStates.Contains(stateCode))
                return (false, "Invalid state code (must be 01-16).");

            return (true, "Valid IC number.");
        }

        /// <summary>
        /// Validates password strength requirements
        /// </summary>
        public (bool isValid, string errorMessage) ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return (false, "Password cannot be empty.");

            if (password.Length < 8)
                return (false, "Password must be at least 8 characters.");

            if (!Regex.IsMatch(password, @"[A-Z]"))
                return (false, "Password must contain at least 1 uppercase letter.");

            if (!Regex.IsMatch(password, @"[a-z]"))
                return (false, "Password must contain at least 1 lowercase letter.");

            if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
                return (false, "Password must contain at least 1 symbol.");

            return (true, "Valid password.");
        }

        /// <summary>
        /// Validates phone number format
        /// </summary>
        public (bool isValid, string errorMessage) ValidatePhoneNumber(string phoneNumber)
        {
            string formattedPhone = _phoneService.FormatPhoneNumber(phoneNumber);

            if (!_phoneService.ValidatePhoneFormat(formattedPhone))
                return (false, "Phone number must be in format 01X-XXXXXXX or 01X-XXXXXXXX.");

            if (!_phoneService.IsPhoneNumberAvailable(phoneNumber))
                return (false, "Phone number already registered.");

            return (true, "Valid phone number.");
        }
    }
}