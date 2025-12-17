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

            // Format phone number
            string formattedPhoneNumber = FormatPhoneNumber(model.PhoneNumber);

            // Validate phone number format
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

            // Set session for authenticated user
            HttpContext.Session.SetString("CustomerId", user.UserId);
            HttpContext.Session.SetString("CustomerName", user.Name);
            HttpContext.Session.SetString("CustomerPhone", user.Phone);

            // Set "Remember Me" cookie if checked
            if (model.RememberMe)
            {
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
        /// Validates phone number format
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
        public async Task<IActionResult> ForgotPassword(string phoneNumber, string email, string verificationCode, string action)
        {
            // Format phone numbers
            string formattedPhoneNumber = FormatPhoneNumber(phoneNumber);

            // Validate formatted phone number
            if (!ValidatePhoneFormat(formattedPhoneNumber))
            {
                ViewData["Error"] = "Invalid phone number format. Use 01X-XXXXXXX or 01X-XXXXXXXX";
                return View();
            }

            // ACTION 1: Send Verification Code
            if (action == "send" || string.IsNullOrEmpty(verificationCode))
            {
                if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(email))
                {
                    ViewData["Error"] = "Phone number and email are required.";
                    return View();
                }

                // Find customer by phone and email
                var user = _dbContext.Customers
                    .FirstOrDefault(u => u.Phone == formattedPhoneNumber && u.Email == email && u.Role == "customer");

                if (user != null)
                {
                    // Generate 6-digit verification code
                    string code = GenerateVerificationCode();

                    // Clear any existing codes for this user
                    var existingTokens = _dbContext.PasswordResetTokens
                        .Where(t => t.CustomerId == user.UserId && !t.IsVerified)
                        .ToList();
                    _dbContext.PasswordResetTokens.RemoveRange(existingTokens);

                    // Create new verification code entry
                    var resetToken = new PasswordResetToken
                    {
                        CustomerId = user.UserId,
                        Email = email,
                        Phone = formattedPhoneNumber,
                        VerificationCode = code,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                        IsVerified = false,
                        AttemptCount = 0
                    };

                    _dbContext.PasswordResetTokens.Add(resetToken);
                    _dbContext.SaveChanges();

                    // Send verification code email
                    try
                    {
                        await _emailService.SendVerificationCodeEmailAsync(user.Email, user.Name, code);
                        Console.WriteLine($"[VERIFICATION CODE] Code sent to {user.Email}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[VERIFICATION CODE ERROR] Failed to send code: {ex.Message}");
                    }

                    ViewData["CodeSent"] = true;
                    ViewData["Email"] = email;
                    ViewData["Phone"] = formattedPhoneNumber;
                    ViewData["Success"] = "Verification code sent to your email. Please check your inbox.";
                }
                else
                {
                    ViewData["Success"] = "If an account exists with that phone number and email, a verification code will be sent.";
                }

                return View();
            }

            // ACTION 2: Verify Code
            if (action == "verify" && !string.IsNullOrEmpty(verificationCode))
            {
                if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(email))
                {
                    ViewData["Error"] = "Phone number and email are required.";
                    return View();
                }

                // Find the verification code
                var resetToken = _dbContext.PasswordResetTokens
                    .FirstOrDefault(t => t.Email == email &&
                                        t.Phone == formattedPhoneNumber &&
                                        t.VerificationCode == verificationCode &&
                                        !t.IsVerified &&
                                        t.ExpiresAt > DateTime.UtcNow);

                if (resetToken == null)
                {
                    resetToken = _dbContext.PasswordResetTokens
                        .FirstOrDefault(t => t.Email == email &&
                                            t.Phone == formattedPhoneNumber &&
                                            !t.IsVerified);

                    if (resetToken != null)
                    {
                        resetToken.AttemptCount++;
                        _dbContext.SaveChanges();
                    }

                    ViewData["Error"] = "Invalid verification code. Please try again.";
                    ViewData["CodeSent"] = true;
                    ViewData["Email"] = email;
                    ViewData["Phone"] = formattedPhoneNumber;
                    return View();
                }

                // Mark code as verified
                resetToken.IsVerified = true;
                resetToken.VerifiedAt = DateTime.UtcNow;
                _dbContext.SaveChanges();

                // Store in session for password reset
                HttpContext.Session.SetString("ResetCustomerId", resetToken.CustomerId);
                HttpContext.Session.SetString("ResetEmail", email);
                HttpContext.Session.SetString("ResetPhone", formattedPhoneNumber);

                return RedirectToAction("ResetPassword");
            }

            return View();
        }

        public IActionResult ResetPassword()
        {
            // Check if user has verified the code
            var customerId = HttpContext.Session.GetString("ResetCustomerId");

            if (string.IsNullOrEmpty(customerId))
            {
                return RedirectToAction("ForgotPassword");
            }

            return View();
        }

        [HttpPost]
        public IActionResult ResetPassword(string newPassword, string confirmPassword)
        {
            // Get customer ID from session
            var customerId = HttpContext.Session.GetString("ResetCustomerId");
            var resetEmail = HttpContext.Session.GetString("ResetEmail");
            var resetPhone = HttpContext.Session.GetString("ResetPhone");

            if (string.IsNullOrEmpty(customerId))
            {
                return RedirectToAction("ForgotPassword");
            }

            // Validate input
            if (string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                ViewData["Error"] = "Password and confirmation are required.";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewData["Error"] = "Passwords do not match.";
                return View();
            }

            if (newPassword.Length < 8)
            {
                ViewData["Error"] = "Password must be at least 8 characters.";
                return View();
            }

            // Get customer and update password
            var customer = _dbContext.Customers.FirstOrDefault(c => c.UserId == customerId);

            if (customer == null)
            {
                ViewData["Error"] = "User not found.";
                return View();
            }

            // Update password
            customer.Password = newPassword;
            _dbContext.SaveChanges();

            // Clear session
            HttpContext.Session.Remove("ResetCustomerId");
            HttpContext.Session.Remove("ResetEmail");
            HttpContext.Session.Remove("ResetPhone");

            TempData["Success"] = "Your password has been reset successfully. Please login with your new password.";
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

            // Format phone number first
            string formattedPhone = FormatPhoneNumber(model.PhoneNumber);

            // Check if phone number already exists in database
            if (_dbContext.Users.Any(u => u.Phone == formattedPhone))
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
                    Phone = formattedPhone,
                    Password = model.Password,
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
            try
            {
                Console.WriteLine($"\n\n{'='*60}");
                Console.WriteLine($"CheckPhoneNumber CALLED");
                Console.WriteLine($"{'='*60}");
                Console.WriteLine($"Input phoneNumber: '{phoneNumber}'");
                Console.WriteLine($"Input type: {phoneNumber?.GetType().Name ?? "NULL"}");
                Console.WriteLine($"Input length: {phoneNumber?.Length ?? 0}");

                if (string.IsNullOrEmpty(phoneNumber))
                {
                    Console.WriteLine($"ERROR: phoneNumber is null or empty - returning available: true");
                    return Json(new { available = true });
                }

                string cleanedInput = System.Text.RegularExpressions.Regex.Replace(phoneNumber, @"\D", "");
                Console.WriteLine($"Cleaned input: '{cleanedInput}'");

                if (cleanedInput.Length < 10)
                {
                    Console.WriteLine($"ERROR: Cleaned input length {cleanedInput.Length} < 10 - returning available: false");
                    return Json(new { available = false });
                }

                string formattedInput = FormatPhoneNumber(phoneNumber);
                Console.WriteLine($"Formatted input: '{formattedInput}'");

                // Get ALL users from database
                var allUsers = _dbContext.Users.ToList();
                Console.WriteLine($"\n[DATABASE CHECK] Found {allUsers.Count} total users in database");
                
                if (allUsers.Count == 0)
                {
                    Console.WriteLine($"WARNING: No users in database!");
                    return Json(new { available = true });
                }

                // Log each user
                Console.WriteLine($"\nAll users in database:");
                for (int i = 0; i < allUsers.Count; i++)
                {
                    var user = allUsers[i];
                    Console.WriteLine($"  [{i}] UserId: {user.UserId}, Phone: '{user.Phone}' (Length: {user.Phone?.Length ?? 0})");
                    if (!string.IsNullOrEmpty(user.Phone))
                    {
                        string cleanedDbPhone = System.Text.RegularExpressions.Regex.Replace(user.Phone, @"\D", "");
                        Console.WriteLine($"       Cleaned DB phone: '{cleanedDbPhone}'");
                    }
                }

                // Check direct match
                Console.WriteLine($"\n[DIRECT MATCH] Checking if any user.Phone == '{formattedInput}'");
                bool directMatch = allUsers.Any(u => u.Phone == formattedInput);
                Console.WriteLine($"Direct match result: {directMatch}");

                if (directMatch)
                {
                    Console.WriteLine($"✓ MATCH FOUND! Returning available: false");
                    Console.WriteLine($"{'='*60}\n");
                    return Json(new { available = false });
                }

                // Check cleaned match
                Console.WriteLine($"\n[CLEANED MATCH] Checking cleaned versions...");
                bool cleanedMatch = false;
                foreach (var user in allUsers)
                {
                    if (!string.IsNullOrEmpty(user.Phone))
                    {
                        string cleanedDbPhone = System.Text.RegularExpressions.Regex.Replace(user.Phone, @"\D", "");
                        bool matches = (cleanedDbPhone == cleanedInput);
                        Console.WriteLine($"  Compare: '{cleanedInput}' == '{cleanedDbPhone}' ? {matches}");
                        if (matches)
                        {
                            cleanedMatch = true;
                            Console.WriteLine($"  ✓ MATCH!");
                            break;
                        }
                    }
                }

                Console.WriteLine($"\nCleaned match result: {cleanedMatch}");
                Console.WriteLine($"Final available result: {!cleanedMatch}");
                Console.WriteLine($"{'='*60}\n");

                return Json(new { available = !cleanedMatch });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXCEPTION] {ex.Message}");
                Console.WriteLine($"[EXCEPTION] {ex.StackTrace}");
                Console.WriteLine($"{'='*60}\n");
                return Json(new { available = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Clears expired lockout entries from the login attempts dictionary
        /// </summary>
        private void ClearExpiredLockouts(string phoneNumber)
        {
            if (loginAttempts.ContainsKey(phoneNumber))
            {
                var lockoutInfo = loginAttempts[phoneNumber];
                if (DateTime.UtcNow > lockoutInfo.lockoutUntil)
                {
                    loginAttempts.Remove(phoneNumber);
                }
            }
        }

        /// <summary>
        /// Gets the current lockout information for a phone number
        /// </summary>
        private (bool isLocked, int attempts, DateTime lockoutUntil) GetLockoutInfo(string phoneNumber)
        {
            if (!loginAttempts.ContainsKey(phoneNumber))
            {
                return (false, 0, DateTime.UtcNow);
            }

            var (attempts, lockoutUntil) = loginAttempts[phoneNumber];
            bool isLocked = attempts >= LOCKOUT_THRESHOLD && DateTime.UtcNow < lockoutUntil;

            return (isLocked, attempts, lockoutUntil);
        }

        /// <summary>
        /// Increments failed login attempts for a phone number
        /// </summary>
        private void IncrementFailedAttempts(string phoneNumber)
        {
            if (!loginAttempts.ContainsKey(phoneNumber))
            {
                loginAttempts[phoneNumber] = (1, DateTime.UtcNow.AddMinutes(15));
            }
            else
            {
                var (attempts, _) = loginAttempts[phoneNumber];
                attempts++;

                if (attempts >= LOCKOUT_THRESHOLD)
                {
                    loginAttempts[phoneNumber] = (attempts, DateTime.UtcNow.AddMinutes(15));
                }
                else
                {
                    loginAttempts[phoneNumber] = (attempts, DateTime.UtcNow.AddMinutes(15));
                }
            }
        }

        /// <summary>
        /// Resets failed login attempts for a phone number after successful login
        /// </summary>
        private void ResetFailedAttempts(string phoneNumber)
        {
            if (loginAttempts.ContainsKey(phoneNumber))
            {
                loginAttempts.Remove(phoneNumber);
            }
        }

        /// <summary>
        /// Generates a random 6-digit verification code
        /// </summary>
        private string GenerateVerificationCode()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}