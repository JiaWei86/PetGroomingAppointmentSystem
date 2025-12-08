using Microsoft.AspNetCore.Mvc;
using PetGroomingAppointmentSystem.Services;
using PetGroomingAppointmentSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace PetGroomingAppointmentSystem.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class AuthController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly DB _dbContext;

        private static Dictionary<string, (int attempts, DateTime lockoutUntil)> loginAttempts = new();

        private const int LOCKOUT_THRESHOLD = 3;

        public AuthController(IEmailService emailService, DB dbContext)
        {
            _emailService = emailService;
            _dbContext = dbContext;
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

            // Query database for customer user
            var user = _dbContext.Customers
                .FirstOrDefault(u => u.Phone == phoneNumber && u.Password == password && u.Role == "customer");

            if (user == null)
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

            HttpContext.Session.SetString("CustomerId", user.UserId);
            HttpContext.Session.SetString("CustomerName", user.Name);
            HttpContext.Session.SetString("CustomerPhone", user.Phone);

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

            var user = _dbContext.Users
                .FirstOrDefault(u => u.Phone == phoneNumber && u.Email == email && u.Role == "customer");

            if (user != null)
            {
                var resetToken = GenerateResetToken();
                var resetLink = Url.Action("ResetPassword", "Auth", new { token = resetToken }, Request.Scheme);

                try
                {
                    await _emailService.SendPasswordResetEmailAsync(user.Email, user.Name, resetLink ?? "");
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

            // Check if phone number already exists in database
            if (_dbContext.Users.Any(u => u.Phone == phoneNumber))
            {
                ViewData["Error"] = "Phone number already registered.";
                return View();
            }

            try
            {
                // Generate new Customer ID (e.g., C001, C002, etc.)
                var lastCustomer = _dbContext.Customers
                    .OrderByDescending(c => c.UserId)
                    .FirstOrDefault();
                
                string lastId = lastCustomer?.UserId ?? "C000";
                int newIdNumber = int.Parse(lastId.Substring(1)) + 1;
                string newUserId = $"C{newIdNumber:D3}";

                // Create new Customer record (inherits from User)
                var newCustomer = new Models.Customer
                {
                    UserId = newUserId,
                    Name = name,
                    IC = ic,
                    Email = email,
                    Phone = phoneNumber,
                    Password = password, // TODO: Hash password using bcrypt
                    Role = "customer",
                    CreatedAt = DateTime.UtcNow,
                    LoyaltyPoint = 0,
                    Status = "active",
                    RegisteredDate = DateTime.UtcNow
                };

                // Save to database
                _dbContext.Customers.Add(newCustomer);
                _dbContext.SaveChanges();

                ViewData["Success"] = "Registration successful! Please login.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                ViewData["Error"] = $"Registration failed: {ex.Message}";
                return View();
            }
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
}