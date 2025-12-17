using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace PetGroomingAppointmentSystem.Areas.Admin.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Send staff credentials email when new staff is created
    /// </summary>
    public async Task<bool> SendStaffCredentialsEmailAsync(
        string toEmail,
        string staffName,
        string staffId,
        string temporaryPassword,
        string email,
      string phone,
   string loginUrl)
    {
        try
        {
  var subject = "Welcome to Hajimi House Pet Grooming System - Your Account Details";
     var htmlBody = $@"
    <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
           <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
        <h2 style='color: #ff9500; text-align: center;'>Welcome to Hajimi House Pet Grooming!</h2>
      
      <p>Dear <strong>{staffName}</strong>,</p>
  
         <p>Your staff account has been successfully created. Below are your login credentials:</p>
            
     <div style='background-color: #f9f9f9; padding: 20px; border-radius: 8px; margin: 20px 0;'>
        <table style='width: 100%; border-collapse: collapse;'>
    <tr>
             <td style='padding: 8px; font-weight: bold; width: 40%;'>Staff ID:</td>
    <td style='padding: 8px;'>{staffId}</td>
             </tr>
             <tr>
    <td style='padding: 8px; font-weight: bold;'>Email:</td>
       <td style='padding: 8px;'>{email}</td>
      </tr>
                <tr>
    <td style='padding: 8px; font-weight: bold;'>Phone:</td>
<td style='padding: 8px;'>{phone}</td>
             </tr>
            <tr>
        <td style='padding: 8px; font-weight: bold;'>Temporary Password:</td>
         <td style='padding: 8px; color: #ff9500; font-family: monospace; font-size: 16px;'>{temporaryPassword}</td>
       </tr>
        </table>
      </div>
        
        <p><strong> Important Security Notice:</strong></p>
   <ul>
  <li>Please change your password after your first login</li>
       <li>Do not share your credentials with anyone</li>
  <li>Keep this email in a secure location</li>
  </ul>
    
  
        
            <p>If you have any questions, please contact your administrator.</p>
       
         <hr style='border: none; border-top: 1px solid #ddd; margin: 30px 0;' />
  
      <p style='color: #999; font-size: 12px; text-align: center;'>
       Hajimi House Pet Grooming System<br />
        This is an automated email. Please do not reply.
     </p>
             </div>
             </body>
            </html>";

            await SendEmailAsync(toEmail, subject, htmlBody, isHtml: true);
  return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL ERROR] Failed to send staff credentials email: {ex.Message}");
            return false;
     }
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
