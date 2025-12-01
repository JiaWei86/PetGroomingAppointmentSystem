using Microsoft.AspNetCore.Mvc;
using PetGroomingAppointmentSystem.Services;

namespace PetGroomingAppointmentSystem.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class AuthController : Controller
    {
        private readonly IEmailService _emailService;

        private static List<Customer> customers = new()
        {
            new Customer { Id = 1, PhoneNumber = "0123456789", Name = "John Doe", IC = "123456789012", Email = "john@example.com", Password = "password123" }
        };

        private static Dictionary<string, (int attempts, DateTime lockoutUntil)> loginAttempts = new();

        private const int LOCKOUT_THRESHOLD = 3;

        public AuthController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string phoneNumber, string password)
        {
            if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(password))
            {
                ViewData["Error"] = "Phone number and password are required.";
                return View();
            }

            // Clear expired lockouts before checking
            ClearExpiredLockouts(phoneNumber);

            var lockoutInfo = GetLockoutInfo(phoneNumber);

            // Check if account is currently locked
            if (lockoutInfo.isLocked)
            {
                var remainingSeconds = (int)(lockoutInfo.lockoutUntil - DateTime.UtcNow).TotalSeconds;
                ViewData["IsLocked"] = true;
                ViewData["LockoutSeconds"] = Math.Max(0, remainingSeconds);
                ViewData["Error"] = "Too many login attempts. Please try again later.";
                return View();
            }

            // Verify credentials
            var customer = customers.FirstOrDefault(c => c.PhoneNumber == phoneNumber && c.Password == password);

            if (customer == null)
            {
                // Invalid credentials - increment failed attempts
                IncrementFailedAttempts(phoneNumber);
                var updatedLockoutInfo = GetLockoutInfo(phoneNumber);

                if (updatedLockoutInfo.isLocked)
                {
                    ViewData["IsLocked"] = true;
                    ViewData["LockoutSeconds"] = (int)(updatedLockoutInfo.lockoutUntil - DateTime.UtcNow).TotalSeconds;
                    ViewData["Error"] = "Too many login attempts. Please try again later.";
                }
                else
                {
                    ViewData["Error"] = $"Invalid phone number or password. Attempt {updatedLockoutInfo.attempts}/{LOCKOUT_THRESHOLD}.";
                }

                return View();
            }

            // Successful login - reset failed attempts
            ResetFailedAttempts(phoneNumber);

            HttpContext.Session.SetString("CustomerId", customer.Id.ToString());
            HttpContext.Session.SetString("CustomerName", customer.Name);
            HttpContext.Session.SetString("CustomerPhone", customer.PhoneNumber);

            return RedirectToAction("Index", "Home");
        }

        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string phoneNumber, string email)
        {
            if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(email))
            {
                ViewData["Error"] = "Phone number and email are required.";
                return View();
            }

            var customer = customers.FirstOrDefault(c => c.PhoneNumber == phoneNumber && c.Email == email);

            if (customer != null)
            {
                var resetToken = GenerateResetToken();
                var resetLink = Url.Action("ResetPassword", "Auth", new { token = resetToken }, Request.Scheme);

                try
                {
                    await _emailService.SendPasswordResetEmailAsync(customer.Email, customer.Name, resetLink);
                }
                catch
                {
                    // Log error but still show success message for security
                }
            }

            ViewData["Success"] = "If an account exists with that phone number and email, a password reset link will be sent.";
            return View();
        }

        public IActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                ViewData["Error"] = "Invalid reset token.";
                return View();
            }

            ViewData["ResetToken"] = token;
            return View();
        }

        [HttpPost]
        public IActionResult ResetPassword(string token, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                ViewData["Error"] = "Password and confirmation are required.";
                ViewData["ResetToken"] = token;
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewData["Error"] = "Passwords do not match.";
                ViewData["ResetToken"] = token;
                return View();
            }

            if (newPassword.Length < 8)
            {
                ViewData["Error"] = "Password must be at least 6 characters.";
                ViewData["ResetToken"] = token;
                return View();
            }

            ViewData["Success"] = "Your password has been reset successfully. Please login with your new password.";
            return RedirectToAction("Login");
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string phoneNumber, string name, string ic, string email, string password, string confirmPassword)
        {
            if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(name) || string.IsNullOrEmpty(ic) ||
                string.IsNullOrEmpty(email) || string.IsNullOrEmpty(confirmPassword))
            {
                ViewData["Error"] = "All fields are required.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewData["Error"] = "Passwords do not match.";
                return View();
            }

            if (password.Length < 6)
            {
                ViewData["Error"] = "Password must be at least 6 characters.";
                return View();
            }

            if (customers.Any(c => c.PhoneNumber == phoneNumber))
            {
                ViewData["Error"] = "Phone number already registered.";
                return View();
            }

            var newCustomer = new Customer
            {
                Id = customers.Count + 1,
                PhoneNumber = phoneNumber,
                Name = name,
                IC = ic,
                Email = email,
                Password = password
            };

            customers.Add(newCustomer);

            ViewData["Success"] = "Registration successful! Please login.";
            return RedirectToAction("Login");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private (int attempts, DateTime lockoutUntil, bool isLocked) GetLockoutInfo(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber) || !loginAttempts.TryGetValue(phoneNumber, out var info))
            {
                return (0, DateTime.UtcNow, false);
            }

            bool isLocked = DateTime.UtcNow < info.lockoutUntil;
            return (info.attempts, info.lockoutUntil, isLocked);
        }

        private void IncrementFailedAttempts(string phoneNumber)
        {
            if (!loginAttempts.TryGetValue(phoneNumber, out var info))
            {
                info = (0, DateTime.UtcNow);
            }

            int newAttempts = info.attempts + 1;
            DateTime lockoutUntil = info.lockoutUntil;

            if (newAttempts >= LOCKOUT_THRESHOLD)
            {
                int lockoutSeconds = CalculateLockoutSeconds(newAttempts - LOCKOUT_THRESHOLD);
                lockoutUntil = DateTime.UtcNow.AddSeconds(lockoutSeconds);
            }

            loginAttempts[phoneNumber] = (newAttempts, lockoutUntil);
        }

        private void ResetFailedAttempts(string phoneNumber)
        {
            if (!string.IsNullOrEmpty(phoneNumber))
            {
                loginAttempts.Remove(phoneNumber);
            }
        }

        private void ClearExpiredLockouts(string phoneNumber)
        {
            if (!string.IsNullOrEmpty(phoneNumber) && loginAttempts.TryGetValue(phoneNumber, out var info))
            {
                if (DateTime.UtcNow >= info.lockoutUntil && info.attempts >= LOCKOUT_THRESHOLD)
                {
                    loginAttempts.Remove(phoneNumber);
                }
            }
        }

        private int CalculateLockoutSeconds(int lockoutCount)
        {
            return 15 * (int)Math.Pow(2, lockoutCount);
        }

        private string GenerateResetToken()
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    public class Customer
    {
        public int Id { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Name { get; set; }
        public required string IC { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}