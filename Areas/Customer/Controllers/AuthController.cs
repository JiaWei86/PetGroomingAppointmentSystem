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
        private readonly IRecaptchaService _recaptchaService;
        private readonly IPasswordService _passwordService;  

        private static Dictionary<string, (int attempts, int secondsRemaining)> loginAttempts = new();
        private static bool timerStarted = false;
        private static readonly object timerLock = new object();  

        private const int LOCKOUT_THRESHOLD = 3;
        private const int LOCKOUT_DURATION_SECONDS = 15;

        public AuthController(
            IEmailService emailService,
            DB dbContext,
            IPhoneService phoneService,
            IValidationService validationService,
            IRecaptchaService recaptchaService,
            IPasswordService passwordService)  
        {
            _emailService = emailService;
            _dbContext = dbContext;
            _phoneService = phoneService;
            _validationService = validationService;
            _recaptchaService = recaptchaService;
            _passwordService = passwordService;  
            
            
            if (!timerStarted)
            {
                lock (timerLock)
                {
                    if (!timerStarted)
                    {
                        timerStarted = true;
                        StartLockoutTimerThread();
                        Console.WriteLine("[TIMER] Lockout timer thread started");
                    }
                }
            }
        }

        
        private static void StartLockoutTimerThread()
        {
            Thread timerThread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(1000);  

                    lock (loginAttempts)
                    {
                        var expiredKeys = new List<string>();

                        foreach (var key in loginAttempts.Keys.ToList())
                        {
                            var (attempts, secondsRemaining) = loginAttempts[key];
                            secondsRemaining--;

                            if (secondsRemaining <= 0)
                            {
                                expiredKeys.Add(key);
                            }
                            else
                            {
                                loginAttempts[key] = (attempts, secondsRemaining);
                            }
                        }

                        
                        foreach (var key in expiredKeys)
                        {
                            loginAttempts.Remove(key);
                            Console.WriteLine($"[TIMEOUT] {key}: Lockout expired");
                        }
                    }
                }
            })
            {
                IsBackground = true
            };

            timerThread.Start();
        }

        public IActionResult Login()
        {
            
            ViewData.Remove("Error");
            ViewData.Remove("IsLocked");
            ViewData.Remove("LockoutSeconds");
            
           
            var recaptchaSiteKey = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["RecaptchaSettings:SiteKey"];
            ViewData["RecaptchaSiteKey"] = recaptchaSiteKey;
            ViewData["RequireRecaptcha"] = !IsMobileDevice();  
            
            var model = new LoginViewModel();
            
            if (Request.Cookies.TryGetValue("RememberPhone", out string rememberPhone))
            {
                model.PhoneNumber = rememberPhone;
                model.RememberMe = true;
            }
            
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string recaptchaToken = null)
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

            // ✅ 只在非移动设备上检查 reCAPTCHA
            if (!IsMobileDevice())
            {
                if (string.IsNullOrWhiteSpace(recaptchaToken) || recaptchaToken == null)
                {
                    recaptchaToken = Request.Form["g-recaptcha-response"].ToString();
                }

                if (string.IsNullOrWhiteSpace(recaptchaToken))
                {
                    ModelState.AddModelError("", "Please complete the reCAPTCHA verification");
                    ViewData["RequireRecaptcha"] = true;
                    var siteKey = HttpContext.RequestServices
                        .GetRequiredService<IConfiguration>()["RecaptchaSettings:SiteKey"];
                    ViewData["RecaptchaSiteKey"] = siteKey;
                    return View(model);
                }

                bool recaptchaValid = await _recaptchaService.VerifyTokenAsync(recaptchaToken);
                if (!recaptchaValid)
                {
                    ModelState.AddModelError("", "reCAPTCHA verification failed. Please try again.");
                    ViewData["RequireRecaptcha"] = true;
                    var siteKey = HttpContext.RequestServices
                        .GetRequiredService<IConfiguration>()["RecaptchaSettings:SiteKey"];
                    ViewData["RecaptchaSiteKey"] = siteKey;
                    return View(model);
                }
            }

            
            var lockoutInfo = GetLockoutInfo(formattedPhoneNumber);

            if (lockoutInfo.isLocked)
            {
                ViewData["IsLocked"] = true;
                ViewData["LockoutSeconds"] = lockoutInfo.secondsRemaining; 
                ViewData["Error"] = "Too many login attempts. Please try again later.";
                Console.WriteLine($"[LOCKED] {formattedPhoneNumber}: {lockoutInfo.secondsRemaining}s remaining");
                return View(model);
            }

            // Query database
            var user = _dbContext.Customers
                .FirstOrDefault(u => u.Phone == formattedPhoneNumber && u.Role == "customer");

            if (user == null || !_passwordService.VerifyPassword(model.Password, user.Password))
            {
               
                IncrementFailedAttempts(formattedPhoneNumber);
                
                var updatedLockoutInfo = GetLockoutInfo(formattedPhoneNumber);

                if (updatedLockoutInfo.isLocked)
                {
                    ViewData["IsLocked"] = true;
                    ViewData["LockoutSeconds"] = updatedLockoutInfo.secondsRemaining;  
                    ViewData["Error"] = "Too many login attempts. Please try again later.";
                    Console.WriteLine($"[LOCKED NOW] {formattedPhoneNumber}");
                }
                else
                {
                    ViewData["Error"] = $"Invalid phone number or password. Attempt {updatedLockoutInfo.attempts}/3.";
                    
                    
                    if (!IsMobileDevice())
                    {
                        ViewData["RequireRecaptcha"] = true;
                        var siteKey = HttpContext.RequestServices
                            .GetRequiredService<IConfiguration>()["RecaptchaSettings:SiteKey"];
                        ViewData["RecaptchaSiteKey"] = siteKey;
                    }
                }

                return View(model);
            }

            
            ResetFailedAttempts(formattedPhoneNumber);
            Console.WriteLine($"[LOGIN SUCCESS] {formattedPhoneNumber}: Reset attempts");

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
                    Secure = HttpContext.Request.IsHttps,
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict
                });
            }

            // ✅ Check if customer needs to set a new password
            if (user.Status == "pending_password")
            {
                HttpContext.Session.SetString("CustomerId", user.UserId);
                HttpContext.Session.SetString("CustomerName", user.Name);
                HttpContext.Session.SetString("CustomerPhone", user.Phone);
                TempData["ShowChangePassword"] = true;
                return RedirectToAction("Profile", "Home");
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

            
            customer.Password = _passwordService.HashPassword(newPassword);
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

                
                string hashedPassword = _passwordService.HashPassword(model.Password);

                // Create new Customer record (inherits from User)
                var newCustomer = new Models.Customer
                {
                    UserId = newUserId,
                    Name = model.Name,
                    IC = model.IC,
                    Email = model.Email,
                    Phone = formattedPhone,
                    Password = hashedPassword,  
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
                    
                    HttpContext.Session.SetString("JustRegisteredPhone", formattedPhone);
                    
                    return Json(new { 
                        success = true, 
                        message = "Registration successful! Redirecting to login...",
                        redirectUrl = Url.Action("Login", "Auth", new { area = "Customer", registered = "true" })
                    });
                }

                
                HttpContext.Session.SetString("JustRegisteredPhone", formattedPhone);
    
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
                if (string.IsNullOrEmpty(phoneNumber))
                {
                    return Json(new { available = true });
                }

                string cleanedInput = System.Text.RegularExpressions.Regex.Replace(phoneNumber, @"\D", "");

                if (cleanedInput.Length < 10)
                {
                    return Json(new { available = false });
                }

                string formattedInput = FormatPhoneNumber(phoneNumber);

                
                bool exists = _dbContext.Users
                    .AsNoTracking()  
                    .Any(u => u.Phone == formattedInput);

                return Json(new { available = !exists });
            }
            catch (Exception ex)
            {
                return Json(new { available = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Gets the current lockout information for a phone number
        /// </summary>
        private (bool isLocked, int attempts, int secondsRemaining) GetLockoutInfo(string phoneNumber)
        {
            try
            {
                lock (loginAttempts)
                {
                    if (!loginAttempts.ContainsKey(phoneNumber))
                        return (false, 0, 0);

                    var (attempts, secondsRemaining) = loginAttempts[phoneNumber];

                    
                    if (secondsRemaining <= 0)
                    {
                        loginAttempts.Remove(phoneNumber);
                        return (false, 0, 0);
                    }

                    bool isLocked = attempts >= LOCKOUT_THRESHOLD;
                    return (isLocked, attempts, secondsRemaining);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR GetLockoutInfo] {ex.Message}");
                return (false, 0, 0);
            }
        }

        /// <summary>
        /// Increments failed login attempts for a phone number
        /// </summary>
        private void IncrementFailedAttempts(string phoneNumber)
        {
            try
            {
                lock (loginAttempts)
                {
                    int attempts = 1;
                    int secondsRemaining = LOCKOUT_DURATION_SECONDS;

                    if (loginAttempts.ContainsKey(phoneNumber))
                    {
                        var (prevAttempts, prevSeconds) = loginAttempts[phoneNumber];

                        
                        if (prevSeconds > 0)
                        {
                            attempts = prevAttempts + 1;
                            secondsRemaining = LOCKOUT_DURATION_SECONDS;  
                        }
                        else
                        {
                            
                            attempts = 1;
                            secondsRemaining = LOCKOUT_DURATION_SECONDS;
                        }
                    }

                    loginAttempts[phoneNumber] = (attempts, secondsRemaining);  
                    Console.WriteLine($"[ATTEMPT] {phoneNumber}: {attempts}/{LOCKOUT_THRESHOLD} - {secondsRemaining}s remaining");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR IncrementFailedAttempts] {ex.Message}");
            }
        }

        /// <summary>
        /// Resets failed login attempts for a phone number (on successful login)
        /// </summary>
        private void ResetFailedAttempts(string phoneNumber)
        {
            try
            {
                lock (loginAttempts)
                {
                    if (loginAttempts.ContainsKey(phoneNumber))
                    {
                        loginAttempts.Remove(phoneNumber);
                        Console.WriteLine($"[RESET] {phoneNumber}: Attempts cleared");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR ResetFailedAttempts] {ex.Message}");
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
            // Remove only customer-specific session keys to avoid clearing admin/staff sessions
            HttpContext.Session.Remove("CustomerId");
            HttpContext.Session.Remove("CustomerName");
            HttpContext.Session.Remove("CustomerPhone");

            // Also remove any password reset/session keys related to customer flows
            HttpContext.Session.Remove("ResetCustomerId");
            HttpContext.Session.Remove("ResetEmail");
            HttpContext.Session.Remove("ResetPhone");

            
            HttpContext.Session.Clear();

            
            // Response.Cookies.Delete("RememberPhone", new Microsoft.AspNetCore.Http.CookieOptions
            // {
            //     HttpOnly = true,
            //     Secure = true,
            //     SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict
            // });

            return RedirectToAction("Index", "Home", new { area = "Customer" });
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

        
        public class ValidateFieldRequest
        {
            public string FieldName { get; set; }
            public string FieldValue { get; set; }
        }

        /// <summary>
        /// AJAX endpoint for real-time field validation during registration
        /// </summary>
        [HttpPost]
        public IActionResult ValidateRegisterField([FromBody] ValidateFieldRequest request)  
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

            return Json(new { isValid = true, errorMessage = "" });  
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

            return Json(new { isValid = true, errorMessage = "" });  
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

            // ✅ ========== VALIDATE AGE (MUST BE AT LEAST 18) ==========
            var birthDate = new DateTime(fullYear, month, day);
            var today = DateTime.Today;
            int age = today.Year - birthDate.Year;

            // Adjust if birthday hasn't occurred yet this year
            if (birthDate.Date > today.AddYears(-age))
            {
                age--;
            }

            // Check if user is at least 18 years old
            if (age < 18)
            {
                return Json(new { isValid = false, errorMessage = "Age must be at least 18 years old." });
            }

            
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

            return Json(new { isValid = true, errorMessage = "" });  
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

            return Json(new { isValid = true, errorMessage = "" });  
        }

        /// <summary>
        /// Validates if phone number is registered (for login page real-time validation)
        /// </summary>
        [HttpPost]
        public IActionResult ValidatePhoneNumberRegistered(string phoneNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    return Json(new { isRegistered = false, message = "Phone number is required." });
                }

                // Format phone number
                string formattedPhone = FormatPhoneNumber(phoneNumber);

                // Validate format
                if (!ValidatePhoneFormat(formattedPhone))
                {
                    return Json(new { isRegistered = false, message = "Invalid phone number format." });
                }

                // Check if phone number exists in database
                var user = _dbContext.Customers
                    .FirstOrDefault(u => u.Phone == formattedPhone && u.Role == "customer");

                if (user != null)
                {
                    return Json(new { isRegistered = true, message = "Phone number is registered." });
                }
                else
                {
                    return Json(new { isRegistered = false, message = "Phone number hasn't registered." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { isRegistered = false, message = $"Validation error: {ex.Message}" });
            }
        }

     
        private bool IsMobileDevice()
        {
            var userAgent = Request.Headers["User-Agent"].ToString().ToLower();
            return userAgent.Contains("android") ||
                   userAgent.Contains("iphone") ||
                   userAgent.Contains("ipad") ||
                   userAgent.Contains("ipod") ||
                   userAgent.Contains("mobile") ||
                   userAgent.Contains("webos") ||
                   userAgent.Contains("blackberry");
        }
    }
}