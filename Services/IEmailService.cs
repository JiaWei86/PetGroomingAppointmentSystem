using System.Threading.Tasks;

namespace PetGroomingAppointmentSystem.Services
{
    public interface IEmailService
    {
        /// <summary>
        /// Send staff credentials email when new staff is created
        /// </summary>
        Task<bool> SendStaffCredentialsEmailAsync(
            string toEmail,
            string staffName,
            string staffId,
            string temporaryPassword,
            string email,
            string phone,
            string loginUrl);

        /// <summary>
        /// Send verification code email to customer
        /// </summary>
        Task SendVerificationCodeEmailAsync(string toEmail, string toName, string verificationCode);

        /// <summary>
        /// Send password reset email to customer
        /// </summary>
        Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);

        /// <summary>
        /// Send generic email
        /// </summary>
        Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true);
    }
}