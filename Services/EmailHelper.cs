using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace PetGroomingAppointmentSystem.Services
{
    public class EmailHelper
    {
        private readonly IConfiguration _configuration;

        public EmailHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ==================== EMAIL METHODS ====================
        public void SendEmail(MailMessage mail)
        {
            string user = _configuration["Smtp:User"] ?? "";
            string pass = _configuration["Smtp:Pass"] ?? "";
            string host = _configuration["Smtp:Host"] ?? "";
            int port = _configuration.GetValue<int>("Smtp:Port");

            using var smtpClient = new SmtpClient
            {
                Host = host,
                Port = port,
                EnableSsl = false,
                Credentials = new NetworkCredential(user, pass),
                Timeout = 10000
            };

            smtpClient.Send(mail);
        }

        public string GetSenderEmail()
        {
            return _configuration["Smtp:User"] ?? "";
        }

        public string GetSenderName()
        {
            return _configuration["Smtp:Name"] ?? "";
        }

        // ==================== PASSWORD HASHING METHODS ====================
        /// <summary>
        /// Hashes a password using SHA256
        /// </summary>
        public string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        /// <summary>
        /// Verifies if a given password matches the hash
        /// </summary>
        public bool VerifyPassword(string hash, string password)
        {
            var hashOfInput = HashPassword(password);
            return hash == hashOfInput;
        }

        // ==================== RANDOM PASSWORD METHOD ====================
        /// <summary>
        /// Generates a random 10-character password from 36 characters (0-9, A-Z)
        /// </summary>
        public string RandomPassword()
        {
            string s = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string password = "";
            Random r = new Random();

            for (int i = 1; i <= 10; i++)
            {
                password += s[r.Next(s.Length)];
            }

            return password;
        }
    }
}