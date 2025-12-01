using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace PetGroomingAppointmentSystem.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
        {
            var subject = "Password Reset Request";
            var htmlBody = $@"
                <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2 style='color: #ff9500;'>Password Reset Request</h2>
                        <p>Hello {toName},</p>
                        <p>We received a request to reset your password. Click the button below to proceed:</p>
                        <p>
                            <a href='{resetLink}' style='background-color: #ff9500; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                Reset Password
                            </a>
                        </p>
                        <p>Or copy and paste this link:</p>
                        <p>{resetLink}</p>
                        <p>This link will expire in 24 hours.</p>
                        <p>If you did not request this, please ignore this email.</p>
                        <hr>
                        <p style='color: #999; font-size: 12px;'>Pet Grooming Appointment System</p>
                    </body>
                </html>";

            await SendEmailAsync(toEmail, subject, htmlBody, isHtml: true);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"];
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
                var senderEmail = _configuration["EmailSettings:SenderEmail"];
                var senderPassword = _configuration["EmailSettings:SenderPassword"];
                var senderName = _configuration["EmailSettings:SenderName"];

                // Validate configuration
                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                {
                    Console.WriteLine("[EMAIL ERROR] SMTP settings are not configured properly.");
                    return;
                }

                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                    client.Timeout = 10000;

                    using (var mailMessage = new MailMessage())
                    {
                        mailMessage.From = new MailAddress(senderEmail, senderName);
                        mailMessage.To.Add(new MailAddress(toEmail));
                        mailMessage.Subject = subject;
                        mailMessage.Body = body;
                        mailMessage.IsBodyHtml = isHtml;

                        await client.SendMailAsync(mailMessage);
                        Console.WriteLine($"[EMAIL SUCCESS] Email sent to {toEmail}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Failed to send email to {toEmail}: {ex.Message}");
                // Log error but don't throw to prevent application crash
            }
        }
    }
}