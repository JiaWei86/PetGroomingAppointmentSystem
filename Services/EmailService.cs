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

        public async Task<bool> SendStaffCredentialsEmailAsync(string toEmail, string staffName, string staffId, string temporaryPassword, string email, string phone, string loginUrl)
        {
            try
            {
                var subject = "Your Staff Account Credentials";
                var htmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2 style='color: #d97706;'>Welcome {staffName}</h2>
                        <p>Your staff account has been created. Use the credentials below to login:</p>
                        <ul>
                            <li><strong>Staff ID:</strong> {staffId}</li>
                            <li><strong>Email:</strong> {email}</li>
                            <li><strong>Temporary Password:</strong> {temporaryPassword}</li>
                            <li><strong>Phone:</strong> {phone}</li>
                        </ul>
                        <p>You can login here: <a href='{loginUrl}'>{loginUrl}</a></p>
                        <p>Please change your password after first login.</p>
                        <hr>
                        <p style='color: #999; font-size:12px;'>Pet Grooming Appointment System</p>
                    </body>
                    </html>";

                await SendEmailAsync(toEmail, subject, htmlBody, isHtml: true);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] SendStaffCredentialsEmailAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task SendVerificationCodeEmailAsync(string toEmail, string toName, string verificationCode)
        {
            var subject = "Verification Code";
            var htmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #d97706;'>Verification Code</h2>
                    <p>Hello {toName},</p>
                    <p>Your verification code is:</p>
                    <p style='background-color: #f0f0f0; padding:15px; text-align: center; font-size:24px; font-weight: bold; color: #d97706; border-radius:5px; letter-spacing:5px;'>
                        {verificationCode}
                    </p>
                    <p>This code will expire shortly.</p>
                    <hr>
                    <p style='color: #999; font-size:12px;'>Pet Grooming Appointment System</p>
                </body>
                </html>";

            await SendEmailAsync(toEmail, subject, htmlBody, isHtml: true);
        }

        public async Task<bool> SendCustomerCredentialsEmailAsync(
            string toEmail,
            string customerName,
            string customerId,
            string temporaryPassword,
            string phone,
            string loginUrl)
        {
            try
            {
                var subject = "Welcome to Hajimi House Pet Grooming - Your Account Details";
                var htmlBody = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                        <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                            <h2 style='color: #ff9500; text-align: center;'>🐾 Welcome to Hajimi House Pet Grooming!</h2>
                            
                            <p>Dear <strong>{customerName}</strong>,</p>
                            
                            <p>Your customer account has been successfully created. Below are your login credentials:</p>
                            
                            <div style='background-color: #f9f9f9; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                                <table style='width: 100%; border-collapse: collapse;'>
                                    <tr>
                                        <td style='padding: 8px; font-weight: bold; width: 40%;'>Customer ID:</td>
                                        <td style='padding: 8px;'>{customerId}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px; font-weight: bold;'>Email:</td>
                                    <td style='padding: 8px;'>{toEmail}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px; font-weight: bold;'>Temporary Password:</td>
                                    <td style='padding: 8px;'>{temporaryPassword}</td>
                                </tr>
                                <tr>
                                    <td style='padding: 8px; font-weight: bold;'>Phone:</td>
                                    <td style='padding: 8px;'>{phone}</td>
                                </tr>
                            </table>
                        </div>
                        
                        <p>You can login here: <a href='{loginUrl}' style='color: #ff9500; font-weight: bold;'>{loginUrl}</a></p>
                        
                        <p>Please change your password after your first login for security.</p>
                        
                        <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'>
                        <p style='color: #999; font-size:12px; text-align: center;'>Pet Grooming Appointment System</p>
                    </div>
                </body>
                </html>";

                await SendEmailAsync(toEmail, subject, htmlBody, isHtml: true);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] SendCustomerCredentialsEmailAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
        {
            var subject = "Password Reset Request";
            var htmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #d97706;'>Password Reset</h2>
                    <p>Hello {toName},</p>
                    <p>Click the link below to reset your password:</p>
                    <p><a href='{resetLink}' style='color: #d97706; font-weight: bold;'>{resetLink}</a></p>
                    <p>This link will expire in 1 hour.</p>
                    <hr>
                    <p style='color: #999; font-size:12px;'>Pet Grooming Appointment System</p>
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
            }
        }
    }
}