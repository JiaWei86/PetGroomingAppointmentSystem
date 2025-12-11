using Microsoft.AspNetCore.Mvc;
using PetGroomingAppointmentSystem.Services;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Areas.Customer.ViewModels;
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
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Format phone number: convert 01X1234567 or 01X-1234567 to 01X-1234567 format
            string formattedPhoneNumber = FormatPhoneNumber(model.PhoneNumber);

            // Validate formatted phone number
            if (!ValidatePhoneFormat(formattedPhoneNumber))
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "Invalid phone number format. Use 01X-XXXXXXX or 01X-XXXXXXXX");
                return View(model);
            }

            // Clear expired lockouts before checking
            ClearExpiredLockouts(formattedPhoneNumber);

            var lockoutInfo = GetLockoutInfo(formattedPhoneNumber);

            // Check if account is currently locked
            if (lockoutInfo.isLocked)
            {
                var remainingSeconds = (int)(lockoutInfo.lockoutUntil - DateTime.UtcNow).TotalSeconds;
                ViewData["IsLocked"] = true;
                ViewData["LockoutSeconds"] = Math.Max(0, remainingSeconds);
                ViewData["Error"] = "Too many login attempts. Please try again later.";
                return View(model);
            }

            // Query database with formatted phone number
            var user = _dbContext.Customers
                .FirstOrDefault(u => u.Phone == formattedPhoneNumber && u.Password == model.Password && u.Role == "customer");

            if (user == null)
            {
                // Invalid credentials - increment failed attempts
                IncrementFailedAttempts(formattedPhoneNumber);
                var updatedLockoutInfo = GetLockoutInfo(formattedPhoneNumber);

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

                return View(model);
            }

            // Successful login - reset failed attempts
            ResetFailedAttempts(formattedPhoneNumber);

            HttpContext.Session.SetString("CustomerId", user.UserId);
            HttpContext.Session.SetString("CustomerName", user.Name);
            HttpContext.Session.SetString("CustomerPhone", user.Phone);

            if (model.RememberMe)
            {
                // Set cookie to remember login for 30 days
                Response.Cookies.Append("RememberPhone", formattedPhoneNumber, new Microsoft.AspNetCore.Http.CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    HttpOnly = true,
                    Secure = true,
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict
                });
            }

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Formats phone number to 01X-XXXXXXX or 01X-XXXXXXXX format
        /// Accepts: 0121234567, 01212345678, 012-1234567, 012-12345678
        /// </summary>
        private string FormatPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return phoneNumber;

            // Remove all non-digits
            string cleaned = System.Text.RegularExpressions.Regex.Replace(phoneNumber, @"\D", "");

            // Format as 01X-XXXXXXX or 01X-XXXXXXXX (3 digits, dash, 7-8 digits)
            if (cleaned.Length == 10)
            {
                // 0121234567 -> 012-1234567
                return cleaned.Substring(0, 3) + "-" + cleaned.Substring(3);
            }
            else if (cleaned.Length == 11)
            {
                // 01212345678 -> 012-12345678
                return cleaned.Substring(0, 3) + "-" + cleaned.Substring(3);
            }

            return cleaned;
        }

        /// <summary>
        /// Validates if phone number is in correct format: 01X-XXXXXXX or 01X-XXXXXXXX
        /// </summary>
        private bool ValidatePhoneFormat(string phoneNumber)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(phoneNumber, @"^01[0-9]-[0-9]{7,8}$");
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
            return View(new RegisterViewModel());
        }

        [HttpPost]
        public IActionResult Register(RegisterViewModel model)
        {
            // Check if AJAX request
            bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            // Validate using model state
            if (!ModelState.IsValid)
            {
                if (isAjaxRequest)
                {
                    // Return validation errors as JSON for AJAX
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                        );

                    return Json(new { success = false, message = "Validation failed", errors = errors });
                }

                // Return view for regular form submission
                return View(model);
            }

            // Check if phone number already exists in database
            if (_dbContext.Users.Any(u => u.Phone == model.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "Phone number already registered.");

                if (isAjaxRequest)
                {
                    return Json(new { success = false, message = "Phone number already registered." });
                }

                ViewData["Error"] = "Phone number already registered.";
                return View(model);
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
                    Name = model.Name,
                    IC = model.IC,
                    Email = model.Email,
                    Phone = model.PhoneNumber,
                    Password = model.Password, // TODO: Hash password using bcrypt
                    Role = "customer",
                    CreatedAt = DateTime.UtcNow,
                    LoyaltyPoint = 0,
                    Status = "active",
                    RegisteredDate = DateTime.UtcNow
                };

                // Save to database
                _dbContext.Customers.Add(newCustomer);
                _dbContext.SaveChanges();

                if (isAjaxRequest)
                {
                    return Json(new { success = true, message = "Registration successful! Redirecting to login..." });
                }

                ViewData["Success"] = "Registration successful! Please login.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                if (isAjaxRequest)
                {
                    return Json(new { success = false, message = $"Registration failed: {ex.Message}" });
                }

                ViewData["Error"] = $"Registration failed: {ex.Message}";
                return View(model);
            }
        }

        [HttpPost]
        public IActionResult CheckPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))                                                  
            {
                var (attempts, lockoutUntil) = loginAttempts[phoneNumber];
                if (DateTime.UtcNow > lockoutUntil && attempts >= LOCKOUT_THRESHOLD)
                {
                    loginAttempts.Remove(phoneNumber);
                }
                return Json(new { available = true });
            }

            // Check if phone number exists in database
            bool phoneExists = _dbContext.Users.Any(u => u.Phone == phoneNumber);

            return Json(new { available = !phoneExists });
        }

        public IActionResult Logout()
        {
            // Store the referrer URL before clearing session
            string returnUrl = Request.Headers["Referer"].ToString();
            
            HttpContext.Session.Clear();
            
            // Set success message in TempData
            TempData["LogoutMessage"] = "Logout successfully";
            
            // If there's a valid return URL, redirect back to it; otherwise go to home
            if (!string.IsNullOrEmpty(returnUrl) && returnUrl.Contains(Request.Host.ToString()))
            {
                return Redirect(returnUrl);
            }
            
            return RedirectToAction("Index", "Home");
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