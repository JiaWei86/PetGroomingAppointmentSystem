using System.Collections.Generic;

namespace PetGroomingAppointmentSystem.Services
{
    /// <summary>
    /// Service interface for phone number operations
    /// </summary>
    public interface IPhoneService
    {
        /// <summary>
        /// Formats phone number to 01X-XXXXXXX or 01X-XXXXXXXX format
        /// </summary>
        /// <param name="phoneNumber">Raw phone number input</param>
        /// <returns>Formatted phone number or original input if null/empty</returns>
        string FormatPhoneNumber(string phoneNumber);

        /// <summary>
        /// Validates phone number format (01X-XXXXXXX or 01X-XXXXXXXX)
        /// </summary>
        /// <param name="phoneNumber">Phone number to validate</param>
        /// <returns>True if valid format, false otherwise</returns>
        bool ValidatePhoneFormat(string phoneNumber);

        /// <summary>
        /// Checks if phone number is available (not already registered)
        /// </summary>
        /// <param name="phoneNumber">Phone number to check</param>
        /// <returns>True if available, false if already registered</returns>
        bool IsPhoneNumberAvailable(string phoneNumber);

        /// <summary>
        /// Checks if IC number is available (not already registered)
        /// </summary>
        /// <param name="ic">IC number to check</param>
        /// <returns>True if available, false if already registered</returns>
        bool IsICAvailable(string ic);

        /// <summary>
        /// Clears expired lockout entries
        /// </summary>
        /// <param name="phoneNumber">Phone number to clear lockout for</param>
        void ClearExpiredLockouts(string phoneNumber);

        /// <summary>
        /// Gets lockout information for a phone number
        /// </summary>
        /// <param name="phoneNumber">Phone number to check</param>
        /// <returns>Tuple containing (isLocked, attempts, lockoutUntil)</returns>
        (bool isLocked, int attempts, DateTime lockoutUntil) GetLockoutInfo(string phoneNumber);

        /// <summary>
        /// Increments failed login attempts for a phone number
        /// </summary>
        /// <param name="phoneNumber">Phone number to increment attempts for</param>
        void IncrementFailedAttempts(string phoneNumber);

        /// <summary>
        /// Resets failed login attempts after successful login
        /// </summary>
        /// <param name="phoneNumber">Phone number to reset attempts for</param>
        void ResetFailedAttempts(string phoneNumber);
    }
}