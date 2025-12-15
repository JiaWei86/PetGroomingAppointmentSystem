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

        public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
        {
            var subject = "Password Reset";
            var htmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #d97706;'>Password Reset</h2>
                    <p>Hello {toName},</p>
                    <p>Click the link below to reset your password:</p>
                    <p><a href='{resetLink}'>{resetLink}</a></p>
                    <p>If you didn't request a password reset, please ignore this email.</p>
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
                // Read configuration values
                string user = _configuration["Smtp:User"] ?? "";
                string pass = _configuration["Smtp:Pass"] ?? "";
                string name = _configuration["Smtp:Name"] ?? "";
                string host = _configuration["Smtp:Host"] ?? "";
                int port = _configuration.GetValue<int>("Smtp:Port");

                Console.WriteLine($"[EMAIL DEBUG] Starting email send to {toEmail}");
                Console.WriteLine($"[EMAIL DEBUG] SMTP Host: {host}, Port: {port}");

                // Validate configuration
                if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(host) || port ==0)
                {
                    Console.WriteLine("[EMAIL ERROR] SMTP settings are not configured properly.");
                    Console.WriteLine($"[EMAIL ERROR] User: {(string.IsNullOrEmpty(user) ? "MISSING" : "OK")}");
                    Console.WriteLine($"[EMAIL ERROR] Pass: {(string.IsNullOrEmpty(pass) ? "MISSING" : "OK")}");
                    Console.WriteLine($"[EMAIL ERROR] Host: {(string.IsNullOrEmpty(host) ? "MISSING" : "OK")}");
                    Console.WriteLine($"[EMAIL ERROR] Port: {port}");
                    return;
                }

                // Create mail message
                using (var mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(user, name);
                    mailMessage.To.Add(new MailAddress(toEmail));
                    mailMessage.Subject = subject;
                    mailMessage.Body = body;
                    mailMessage.IsBodyHtml = isHtml;

                    Console.WriteLine($"[EMAIL DEBUG] Mail message created - From: {user}, To: {toEmail}");

                    // Setup SMTP client with credentials
                    using (var smtp = new SmtpClient
                    {
                        Host = host,
                        Port = port,
                        EnableSsl = false, // Port587 uses STARTTLS (not implicit SSL)
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        UseDefaultCredentials = false,
                        Credentials = new NetworkCredential(user, pass),
                        Timeout =20000 // Increased timeout to20 seconds
                    })
                    {
                        // Enable STARTTLS for port587
                        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;

                        try
                        {
                            Console.WriteLine($"[EMAIL DEBUG] Connecting to SMTP server {host}:{port}...");
                            // Send email asynchronously
                            await smtp.SendMailAsync(mailMessage);
                            Console.WriteLine($"[EMAIL SUCCESS] Email sent successfully to {toEmail}");
                        }
                        catch (SmtpException smtpEx)
                        {
                            Console.WriteLine($"[EMAIL ERROR] SMTP Exception: {smtpEx.StatusCode} - {smtpEx.Message}");
                            if (smtpEx.InnerException != null)
                            {
                                Console.WriteLine($"[EMAIL ERROR] Inner Exception: {smtpEx.InnerException.Message}");
                            }
                            throw;
                        }
                        catch (Exception innerEx)
                        {
                            Console.WriteLine($"[EMAIL ERROR] Inner Exception Type: {innerEx.GetType().Name}");
                            Console.WriteLine($"[EMAIL ERROR] Inner Exception: {innerEx.Message}");
                            if (innerEx.InnerException != null)
                            {
                                Console.WriteLine($"[EMAIL ERROR] Inner-Inner Exception: {innerEx.InnerException.Message}");
                            }
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Failed to send email to {toEmail}");
                Console.WriteLine($"[EMAIL ERROR] Exception Type: {ex.GetType().FullName}");
                Console.WriteLine($"[EMAIL ERROR] Exception Message: {ex.Message}");
                Console.WriteLine($"[EMAIL ERROR] Stack Trace: {ex.StackTrace}");
                
                // Log but don't throw - allows app to continue
            }
        }
    }
}