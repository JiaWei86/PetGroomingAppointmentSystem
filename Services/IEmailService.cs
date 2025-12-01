using System.Threading.Tasks;

namespace PetGroomingAppointmentSystem.Services
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);
        Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true);
    }
}