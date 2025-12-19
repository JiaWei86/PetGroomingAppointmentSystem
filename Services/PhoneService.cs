using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PetGroomingAppointmentSystem.Models;

namespace PetGroomingAppointmentSystem.Services
{
    /// <summary>
    /// Service for phone number formatting, validation, and lockout management
    /// </summary>
    public class PhoneService : IPhoneService
    {
        private readonly DB _dbContext;
        private static Dictionary<string, (int attempts, DateTime lockoutUntil)> _loginAttempts = new();
        private const int LOCKOUT_THRESHOLD = 3;
        private const int LOCKOUT_DURATION_SECONDS = 15;

        public PhoneService(DB dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Formats phone number to 01X-XXXXXXX or 01X-XXXXXXXX format
        /// </summary>
        public string FormatPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return phoneNumber;

            // Remove all non-digits
            string cleaned = Regex.Replace(phoneNumber, @"\D", "");

            // Format as 01X-XXXXXXX or 01X-XXXXXXXX (3 digits, dash, 7-8 digits)
            if (cleaned.Length == 10)
            {
                // 0121234567 -> 012-1234567
                return cleaned.Substring(0, 3) + "-" + cleaned.Substring(3);
            }
            else if (cleaned.Length == 11)
            {
                // 01212345678 -> 012-12345678
                return cleaned.Substring(0, 3) + "-" + cleaned.Substring(3);
            }

            return cleaned;
        }

        /// <summary>
        /// Validates phone number format (01X-XXXXXXX or 01X-XXXXXXXX)
        /// </summary>
        public bool ValidatePhoneFormat(string phoneNumber)
        {
            return Regex.IsMatch(phoneNumber, @"^01[0-9]-[0-9]{7,8}$");
        }

        /// <summary>
        /// Checks if phone number is available (not already registered)
        /// </summary>
        public bool IsPhoneNumberAvailable(string phoneNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(phoneNumber))
                    return true;

                string cleanedInput = Regex.Replace(phoneNumber, @"\D", "");

                if (cleanedInput.Length < 10)
                    return false;

                string formattedInput = FormatPhoneNumber(phoneNumber);

                // ✅ 改进：一次性获取所有用户，避免重复查询
                var allUsers = _dbContext.Users.ToList();

                // Check direct match
                bool directMatch = allUsers.Any(u => u.Phone == formattedInput);
                if (directMatch)
                    return false;

                // Check cleaned match
                foreach (var user in allUsers)
                {
                    if (!string.IsNullOrEmpty(user.Phone))
                    {
                        string cleanedDbPhone = Regex.Replace(user.Phone, @"\D", "");
                        if (cleanedDbPhone == cleanedInput)
                            return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PhoneService.IsPhoneNumberAvailable Error] {ex.Message}");
                Console.WriteLine($"[PhoneService.IsPhoneNumberAvailable StackTrace] {ex.StackTrace}");
                // 返回 true（可用）以允许用户继续，而不是完全阻止
                return true;
            }
        }

        /// <summary>
        /// Checks if IC number is available (not already registered)
        /// </summary>
        public bool IsICAvailable(string ic)
        {
            try
            {
                if (string.IsNullOrEmpty(ic))
                    return true;

                // Check if IC already exists in database
                bool icExists = _dbContext.Users.Any(u => u.IC == ic);
                
                return !icExists;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PhoneService.IsICAvailable Error] {ex.Message}");
                Console.WriteLine($"[PhoneService.IsICAvailable StackTrace] {ex.StackTrace}");
                return true;
            }
        }

        /// <summary>
        /// Clears expired lockout entries from the login attempts dictionary
        /// </summary>
        public void ClearExpiredLockouts(string phoneNumber)
        {
            if (_loginAttempts.ContainsKey(phoneNumber))
            {
                var lockoutInfo = _loginAttempts[phoneNumber];
                if (DateTime.UtcNow > lockoutInfo.lockoutUntil)
                {
                    _loginAttempts.Remove(phoneNumber);
                }
            }
        }

        /// <summary>
        /// Gets the current lockout information for a phone number
        /// </summary>
        public (bool isLocked, int attempts, DateTime lockoutUntil) GetLockoutInfo(string phoneNumber)
        {
            if (!_loginAttempts.ContainsKey(phoneNumber))
            {
                return (false, 0, DateTime.UtcNow);
            }

            var (attempts, lockoutUntil) = _loginAttempts[phoneNumber];
            bool isLocked = attempts >= LOCKOUT_THRESHOLD && DateTime.UtcNow < lockoutUntil;

            return (isLocked, attempts, lockoutUntil);
        }

        /// <summary>
        /// Increments failed login attempts for a phone number
        /// </summary>
        public void IncrementFailedAttempts(string phoneNumber)
        {
            if (!_loginAttempts.ContainsKey(phoneNumber))
            {
                _loginAttempts[phoneNumber] = (1, DateTime.UtcNow.AddSeconds(LOCKOUT_DURATION_SECONDS));
            }
            else
            {
                var (attempts, _) = _loginAttempts[phoneNumber];
                attempts++;
                _loginAttempts[phoneNumber] = (attempts, DateTime.UtcNow.AddSeconds(LOCKOUT_DURATION_SECONDS));
            }
        }

        /// <summary>
        /// Resets failed login attempts after successful login
        /// </summary>
        public void ResetFailedAttempts(string phoneNumber)
        {
            if (_loginAttempts.ContainsKey(phoneNumber))
            {
                _loginAttempts.Remove(phoneNumber);
            }
        }
    }
}