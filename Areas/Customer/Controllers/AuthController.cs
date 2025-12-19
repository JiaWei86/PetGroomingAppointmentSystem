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
        private readonly IPhoneService _phoneService;
        private readonly IValidationService _validationService;

        private static Dictionary<string, (int attempts, DateTime lockoutUntil)> loginAttempts = new();

        private const int LOCKOUT_THRESHOLD = 3;

        public AuthController(
            IEmailService emailService,
            DB dbContext,
            IPhoneService phoneService,
            IValidationService validationService)
        {
            _emailService = emailService;
            _dbContext = dbContext;
            _phoneService = phoneService;
            _validationService = validationService;
        }

        public IActionResult Login()
        {
            // ✅ 改进：完全清除所有错误状态
            ViewData.Remove("Error");
            ViewData.Remove("IsLocked");
            ViewData.Remove("LockoutSeconds");
            
            // ✅ 关键改动：检查 "Remember Me" cookie 并填充 Model
            var model = new LoginViewModel();
            
            // 如果有 "RememberPhone" cookie，读取它
            if (Request.Cookies.TryGetValue("RememberPhone", out string rememberPhone))
            {
                model.PhoneNumber = rememberPhone;
                model.RememberMe = true;  // 勾选 Remember Me 复选框
            }
            
            return View(model);
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

            // ✅ 关键：先清除过期的 lockout
            ClearExpiredLockouts(formattedPhoneNumber);
            var lockoutInfo = GetLockoutInfo(formattedPhoneNumber);

            // ✅ 检查 lockout 状态
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
                    ViewData["Error"] = "Too many login attempts. Please wait a moment and try again later.";
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
                    Secure = HttpContext.Request.IsHttps,  // ✅ 改这里：HTTP 开发环境下为 false
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
                    // ✅ 修改：显示错误，而不是成功消息
                    ViewData["Error"] = "No account found with that phone number and email combination. Please verify and try again.";
                    ViewData["Email"] = email;
                    ViewData["Phone"] = formattedPhoneNumber;
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

            // Format phone number using service
            string formattedPhone = _phoneService.FormatPhoneNumber(model.PhoneNumber);

            // Validate phone format using service
            if (!_phoneService.ValidatePhoneFormat(formattedPhone))
            {
                ModelState.AddModelError("PhoneNumber", "Invalid phone number format. Use 01X-XXXXXXX or 01X-XXXXXXXX");
                if (isAjaxRequest)
                {
                    return Json(new { success = false, message = "Invalid phone number format." });
                }
                return View(model);
            }

            // Check if phone number is available
            if (!_phoneService.IsPhoneNumberAvailable(model.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "Phone number already registered.");
                if (isAjaxRequest)
                {
                    return Json(new { success = false, message = "Phone number already registered." });
                }
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
                    // ✅ 返回重定向 URL 给 AJAX 客户端，带上 registered 参数
                    return Json(new { 
                        success = true, 
                        message = "Registration successful! Redirecting to login...",
                        redirectUrl = Url.Action("Login", "Auth", new { area = "Customer", registered = "true" })
                    });
                }

                // ✅ 设置 TempData 来在 Login 页面显示成功消息
                TempData["RegistrationSuccess"] = "Registration successful! Please login with your credentials.";
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
                Console.WriteLine($"\n\n{'=' * 60}");
                Console.WriteLine($"CheckPhoneNumber CALLED");
                Console.WriteLine($"{'=' * 60}");
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
                    Console.WriteLine($"{'=' * 60}\n");
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
                Console.WriteLine($"{'=' * 60}\n");

                return Json(new { available = !cleanedMatch });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXCEPTION] {ex.Message}");
                Console.WriteLine($"[EXCEPTION] {ex.StackTrace}");
                Console.WriteLine($"{'=' * 60}\n");
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
                // ✅ 改进：完全清除过期的lockout，重置尝试次数
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

            // ✅ 新增：如果lockout已过期，重置数据
            if (DateTime.UtcNow > lockoutUntil && attempts >= LOCKOUT_THRESHOLD)
            {
                loginAttempts.Remove(phoneNumber);
                return (false, 0, DateTime.UtcNow);
            }

            return (isLocked, attempts, lockoutUntil);
        }

        /// <summary>
        /// Increments failed login attempts for a phone number
        /// </summary>
        private void IncrementFailedAttempts(string phoneNumber)
        {
            if (!loginAttempts.ContainsKey(phoneNumber))
            {
                // First failed attempt - set lockout to 15 seconds from now
                loginAttempts[phoneNumber] = (1, DateTime.UtcNow.AddSeconds(15));
            }
            else
            {
                var (attempts, currentLockoutUntil) = loginAttempts[phoneNumber];
                
                // ✅ 改进：如果lockout已过期，重置计数器
                if (DateTime.UtcNow > currentLockoutUntil)
                {
                    // Lockout expired - reset to first attempt
                    loginAttempts[phoneNumber] = (1, DateTime.UtcNow.AddSeconds(15));
                }
                else
                {
                    // Still within lockout period - increment
                    attempts++;

                    if (attempts >= LOCKOUT_THRESHOLD)
                    {
                        // Lock account for 15 seconds
                        loginAttempts[phoneNumber] = (attempts, DateTime.UtcNow.AddSeconds(15));
                    }
                    else
                    {
                        // Update attempts but keep the existing lockout timer
                        loginAttempts[phoneNumber] = (attempts, currentLockoutUntil);
                    }
                }
            }
        }

        /// <summary>
        /// Resets failed login attempts for a phone number (on successful login)
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

        [HttpGet]
        public IActionResult Logout()
        {
            // 清除 session
            HttpContext.Session.Clear();

            // ✅ 改动：不删除 RememberPhone cookie，这样下次登录时仍能显示"Welcome back!"
            // Response.Cookies.Delete("RememberPhone", new Microsoft.AspNetCore.Http.CookieOptions
            // {
            //     HttpOnly = true,
            //     Secure = true,
            //     SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict
            // });

            // Redirect to Login page (instead of Home)
            return RedirectToAction("Login", "Auth", new { area = "Customer" });
        }

        /// <summary>
        /// Validates name
        /// </summary>
        [HttpPost]
        public IActionResult ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { isValid = false, errorMessage = "Name cannot be empty." });
            }

            name = name.Trim();

            if (name.Length < 3 || name.Length > 200)
            {
                return Json(new { isValid = false, errorMessage = "Name must be between 3-200 characters." });
            }

            // Check if contains only letters and spaces
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z\s]+$"))
            {
                return Json(new { isValid = false, errorMessage = "Name must contain only letters and spaces." });
            }

            return Json(new { isValid = true, message = "Valid name." });
        }

        /// <summary>
        /// Validates email
        /// </summary>
        [HttpPost]
        public IActionResult ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { isValid = false, errorMessage = "Email cannot be empty." });
            }

            email = email.Trim();

            if (email.Length > 150)
            {
                return Json(new { isValid = false, errorMessage = "Email must not exceed 150 characters." });
            }

            // Check email format
            if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                return Json(new { isValid = false, errorMessage = "Please enter a valid email address." });
            }

            // Check if email already exists
            if (_dbContext.Users.Any(u => u.Email == email))
            {
                return Json(new { isValid = false, errorMessage = "Email already registered." });
            }

            return Json(new { isValid = true, message = "Valid email." });
        }

        /// <summary>
        /// Validates Malaysian IC number
        /// </summary>
        [HttpPost]
        public IActionResult ValidateIC(string ic)
        {
            if (string.IsNullOrWhiteSpace(ic))
            {
                return Json(new { isValid = false, errorMessage = "IC number cannot be empty." });
            }

            ic = ic.Trim();

            // Check format
            if (!System.Text.RegularExpressions.Regex.IsMatch(ic, @"^\d{6}-\d{2}-\d{4}$"))
            {
                return Json(new { isValid = false, errorMessage = "IC number must be in format xxxxxx-xx-xxxx." });
            }

            // Extract date part
            string datePart = ic.Substring(0, 6);
            if (!int.TryParse(datePart.Substring(0, 2), out int year))
                return Json(new { isValid = false, errorMessage = "Invalid year in IC." });
            if (!int.TryParse(datePart.Substring(2, 2), out int month))
                return Json(new { isValid = false, errorMessage = "Invalid month in IC." });
            if (!int.TryParse(datePart.Substring(4, 2), out int day))
                return Json(new { isValid = false, errorMessage = "Invalid day in IC." });

            // Validate month
            if (month < 1 || month > 12)
            {
                return Json(new { isValid = false, errorMessage = "Invalid month in IC (must be 01-12)." });
            }

            // Days in month
            int[] daysInMonth = { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            if (day < 1 || day > daysInMonth[month - 1])
            {
                return Json(new { isValid = false, errorMessage = "Invalid day in IC for the given month." });
            }

            // Convert year
            int fullYear = year >= 50 ? 1900 + year : 2000 + year;

            // Check leap year for Feb 29
            if (month == 2 && day == 29)
            {
                bool isLeapYear = (fullYear % 4 == 0 && fullYear % 100 != 0) || (fullYear % 400 == 0);
                if (!isLeapYear)
                {
                    return Json(new { isValid = false, errorMessage = "Invalid leap year date in IC." });
                }
            }

            // Check date not in future
            int currentYear = DateTime.Now.Year;
            int currentMonth = DateTime.Now.Month;
            int currentDay = DateTime.Now.Day;

            if (fullYear > currentYear || (fullYear == currentYear && month > currentMonth) || (fullYear == currentYear && month == currentMonth && day > currentDay))
            {
                return Json(new { isValid = false, errorMessage = "IC date cannot be in the future." });
            }

            // Validate state code - ✅ 改为 Substring(7, 2)
            string stateCode = ic.Substring(7, 2);
            var validStates = new[] { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15", "16" };
            if (!validStates.Contains(stateCode))
            {
                return Json(new { isValid = false, errorMessage = "Invalid state code (must be 01-16)." });
            }

            return Json(new { isValid = true, message = "Valid IC number." });
        }

        /// <summary>
        /// Validates password
        /// </summary>
        [HttpPost]
        public IActionResult ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return Json(new { isValid = false, errorMessage = "Password cannot be empty." });
            }

            if (password.Length < 8)
            {
                return Json(new { isValid = false, errorMessage = "Password must be at least 8 characters." });
            }

            // Check for uppercase
            if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]"))
            {
                return Json(new { isValid = false, errorMessage = "Password must contain at least 1 uppercase letter." });
            }

            // Check for lowercase
            if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]"))
            {
                return Json(new { isValid = false, errorMessage = "Password must contain at least 1 lowercase letter." });
            }

            // Check for symbol
            if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
            {
                return Json(new { isValid = false, errorMessage = "Password must contain at least 1 symbol." });
            }

            return Json(new { isValid = true, message = "Valid password." });
        }

        // 在 AuthController 类内部添加这个内部类
        public class ValidateFieldRequest
        {
            public string FieldName { get; set; }
            public string FieldValue { get; set; }
        }

        /// <summary>
        /// AJAX endpoint for real-time field validation during registration
        /// </summary>
        [HttpPost]
        public IActionResult ValidateRegisterField([FromBody] ValidateFieldRequest request)  // ✅ 改这里
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.FieldName) || string.IsNullOrWhiteSpace(request.FieldValue))
                {
                    return Json(new { isValid = false, errorMessage = "Invalid validation request." });
                }

                string fieldValue = request.FieldValue.Trim();

                return request.FieldName.ToLower() switch
                {
                    "phonenumber" => ValidatePhoneField(fieldValue),
                    "name" => ValidateNameField(fieldValue),
                    "ic" => ValidateICField(fieldValue),
                    "email" => ValidateEmailField(fieldValue),
                    "password" => ValidatePasswordField(fieldValue),
                    _ => Json(new { isValid = false, errorMessage = "Invalid field for validation." })
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ValidateRegisterField Error] {ex.Message}");
                Console.WriteLine($"[ValidateRegisterField StackTrace] {ex.StackTrace}");
                return Json(new { isValid = false, errorMessage = $"Validation error: {ex.Message}" });
            }
        }

        private IActionResult ValidatePhoneField(string phoneNumber)
        {
            // Format phone using service
            string formattedPhone = _phoneService.FormatPhoneNumber(phoneNumber);

            // Validate format using service
            if (!_phoneService.ValidatePhoneFormat(formattedPhone))
            {
                return Json(new { isValid = false, errorMessage = "Phone must be in format 01X-XXXXXXX or 01X-XXXXXXXX." });
            }

            // Check if already registered using service
            if (!_phoneService.IsPhoneNumberAvailable(phoneNumber))
            {
                return Json(new { isValid = false, errorMessage = "This phone number is already registered." });
            }

            return Json(new { isValid = true, errorMessage = "" });  // ✅ 改这里
        }

        private IActionResult ValidateNameField(string name)
        {
            if (name.Length < 3 || name.Length > 200)
            {
                return Json(new { isValid = false, errorMessage = "Name must be between 3-200 characters." });
            }

            var nameRegex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z\s]+$");
            if (!nameRegex.IsMatch(name))
            {
                return Json(new { isValid = false, errorMessage = "Name must contain only letters and spaces." });
            }

            return Json(new { isValid = true, errorMessage = "" });  // ✅ 改这里
        }

        private IActionResult ValidateICField(string ic)
        {
            // Check format
            var icRegex = new System.Text.RegularExpressions.Regex(@"^\d{6}-\d{2}-\d{4}$");
            if (!icRegex.IsMatch(ic))
            {
                return Json(new { isValid = false, errorMessage = "IC must be in format xxxxxx-xx-xxxx." });
            }

            // Extract date part for additional validation
            string datePart = ic.Substring(0, 6);
            if (!int.TryParse(datePart.Substring(0, 2), out int year) ||
                !int.TryParse(datePart.Substring(2, 2), out int month) ||
                !int.TryParse(datePart.Substring(4, 2), out int day))
            {
                return Json(new { isValid = false, errorMessage = "Invalid date in IC." });
            }

            // Validate month
            if (month < 1 || month > 12)
            {
                return Json(new { isValid = false, errorMessage = "Invalid month in IC (must be 01-12)." });
            }

            // Validate day
            int[] daysInMonth = { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            if (day < 1 || day > daysInMonth[month - 1])
            {
                return Json(new { isValid = false, errorMessage = "Invalid day in IC for the given month." });
            }

            // ✅ Convert year (05 -> 2005, 85 -> 1985)
            int fullYear = year >= 50 ? 1900 + year : 2000 + year;

            // ✅ Check leap year for Feb 29
            if (month == 2 && day == 29)
            {
                bool isLeapYear = (fullYear % 4 == 0 && fullYear % 100 != 0) || (fullYear % 400 == 0);
                if (!isLeapYear)
                {
                    return Json(new { isValid = false, errorMessage = "Invalid leap year date in IC." });
                }
            }

            // ✅ Check date not in future
            int currentYear = DateTime.Now.Year;
            int currentMonth = DateTime.Now.Month;
            int currentDay = DateTime.Now.Day;

            if (fullYear > currentYear || (fullYear == currentYear && month > currentMonth) || (fullYear == currentYear && month == currentMonth && day > currentDay))
            {
                return Json(new { isValid = false, errorMessage = "IC date cannot be in the future." });
            }

            // Validate state code - ✅ 改为 Substring(7, 2)
            string stateCode = ic.Substring(7, 2);
            var validStates = new[] { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15", "16" };
            if (!validStates.Contains(stateCode))
            {
                return Json(new { isValid = false, errorMessage = "Invalid state code (must be 01-16)." });
            }

            // ✅ CHECK IF IC IS ALREADY REGISTERED
            if (!_phoneService.IsICAvailable(ic))
            {
                return Json(new { isValid = false, errorMessage = "This IC number is already registered." });
            }

            return Json(new { isValid = true, errorMessage = "" });
        }

        private IActionResult ValidateEmailField(string email)
        {
            if (email.Length > 150)
            {
                return Json(new { isValid = false, errorMessage = "Email must not exceed 150 characters." });
            }

            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");
            if (!emailRegex.IsMatch(email))
            {
                return Json(new { isValid = false, errorMessage = "Please enter a valid email address." });
            }

            if (_dbContext.Users.Any(u => u.Email == email))
            {
                return Json(new { isValid = false, errorMessage = "This email is already registered." });
            }

            return Json(new { isValid = true, errorMessage = "" });  // ✅ 改这里
        }

        private IActionResult ValidatePasswordField(string password)
        {
            if (password.Length < 8)
            {
                return Json(new { isValid = false, errorMessage = "Password must be at least 8 characters." });
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]"))
            {
                return Json(new { isValid = false, errorMessage = "Password must contain at least 1 uppercase letter." });
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]"))
            {
                return Json(new { isValid = false, errorMessage = "Password must contain at least 1 lowercase letter." });
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
            {
                return Json(new { isValid = false, errorMessage = "Password must contain at least 1 symbol." });
            }

            return Json(new { isValid = true, errorMessage = "" });  // ✅ 改这里
        }
    }
}