namespace PetGroomingAppointmentSystem.Services
{
    /// <summary>
    /// Service interface for field validation operations
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// Validates name format and constraints
        /// </summary>
        /// <param name="name">Name to validate</param>
        /// <returns>Tuple containing (isValid, errorMessage)</returns>
        (bool isValid, string errorMessage) ValidateName(string name);

        /// <summary>
        /// Validates email format and checks if already registered
        /// </summary>
        /// <param name="email">Email to validate</param>
        /// <returns>Tuple containing (isValid, errorMessage)</returns>
        (bool isValid, string errorMessage) ValidateEmail(string email);

        /// <summary>
        /// Validates Malaysian IC number format and content
        /// </summary>
        /// <param name="ic">IC number to validate</param>
        /// <returns>Tuple containing (isValid, errorMessage)</returns>
        (bool isValid, string errorMessage) ValidateIC(string ic);

        /// <summary>
        /// Validates password strength requirements
        /// </summary>
        /// <param name="password">Password to validate</param>
        /// <returns>Tuple containing (isValid, errorMessage)</returns>
        (bool isValid, string errorMessage) ValidatePassword(string password);

        /// <summary>
        /// Validates phone number format
        /// </summary>
        /// <param name="phoneNumber">Phone number to validate</param>
        /// <returns>Tuple containing (isValid, errorMessage)</returns>
        (bool isValid, string errorMessage) ValidatePhoneNumber(string phoneNumber);
    }
}