using Microsoft.AspNetCore.Mvc;
using PetGroomingAppointmentSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace PetGroomingAppointmentSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuthController : Controller
    {
        private readonly DB _dbContext;
        private static Dictionary<string, (int attempts, DateTime lockoutUntil)> loginAttempts = new();
        private const int LOCKOUT_THRESHOLD = 3;
        private const int LOCKOUT_SECONDS = 15;

        // Hardcoded admin credentials - DO NOT CHANGE
        private static readonly Dictionary<string, string> AdminCredentials = new()
        {
            { "Admin", "admin123" }
        };

        public AuthController(DB dbContext)
        {
            _dbContext = dbContext;
        }

        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewData["Error"] = "Username and password are required.";
                return View();
            }

            // Clear expired lockouts before checking
            ClearExpiredLockouts(username);

            var lockoutInfo = GetLockoutInfo(username);

            // Check if account is currently locked
            if (lockoutInfo.isLocked)
            {
                var remainingSeconds = (int)(lockoutInfo.lockoutUntil - DateTime.UtcNow).TotalSeconds;
                ViewData["IsLocked"] = true;
                ViewData["LockoutSeconds"] = Math.Max(0, remainingSeconds);
                ViewData["Error"] = "Too many login attempts. Please try again later.";
                return View();
            }

            // First, try to validate as Admin (hardcoded credentials)
            bool isValidAdmin = ValidateAdmin(username, password);
            if (isValidAdmin)
            {
                // Clear login attempts on successful login
                loginAttempts.Remove(username);

                // Get the Admin record from database to retrieve UserId (not AdminId)
                var adminUser = _dbContext.Admins.FirstOrDefault(a => a.Name == username);
                
                if (adminUser != null)
                {
                    // Set session variables for Admin
                    HttpContext.Session.SetString("AdminUsername", username);
                    HttpContext.Session.SetString("AdminId", adminUser.UserId);  // Use UserId instead of AdminId
                    HttpContext.Session.SetString("UserRole", "admin");
                    HttpContext.Session.SetString("IsAdminLoggedIn", "true");
                }
                else
                {
                    // If admin doesn't exist in database, create error
                    ViewData["Error"] = "Admin account not found in database.";
                    return View();
                }

                return RedirectToAction("Index", "Home");
            }

            // If not admin, try to validate as Staff using database (StaffId as username, IC as password)
            var staffUser = _dbContext.Staffs
                .FirstOrDefault(s => s.UserId == username && s.IC == password && s.Role == "staff");

            if (staffUser != null)
            {
                // Clear login attempts on successful login
                loginAttempts.Remove(username);

                // Set session variables for Staff
                HttpContext.Session.SetString("AdminUsername", staffUser.Name);
                HttpContext.Session.SetString("StaffId", staffUser.UserId);
                HttpContext.Session.SetString("UserRole", "staff");
                HttpContext.Session.SetString("IsAdminLoggedIn", "true");

                return RedirectToAction("Index", "Home");
            }

            // Invalid credentials - increment failed attempts
            IncrementLoginAttempts(username);
            lockoutInfo = GetLockoutInfo(username);

            if (lockoutInfo.isLocked)
            {
                var remainingSeconds = (int)(lockoutInfo.lockoutUntil - DateTime.UtcNow).TotalSeconds;
                ViewData["IsLocked"] = true;
                ViewData["LockoutSeconds"] = Math.Max(0, remainingSeconds);
                ViewData["Error"] = "Too many login attempts. Please try again later.";
            }
            else
            {
                int remainingAttempts = LOCKOUT_THRESHOLD - lockoutInfo.attempts;
                ViewData["Error"] = $"Invalid username or password. {remainingAttempts} attempts remaining.";
            }

            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Remove("AdminUsername");
            HttpContext.Session.Remove("AdminId");
            HttpContext.Session.Remove("StaffId");
            HttpContext.Session.Remove("UserRole");
            HttpContext.Session.Remove("IsAdminLoggedIn");
            return RedirectToAction("Login");
        }

        private bool ValidateAdmin(string username, string password)
        {
            return AdminCredentials.TryGetValue(username, out var storedPassword) && storedPassword == password;
        }

        private void IncrementLoginAttempts(string username)
        {
            if (loginAttempts.ContainsKey(username))
            {
                var (attempts, lockoutUntil) = loginAttempts[username];
                attempts++;

                if (attempts >= LOCKOUT_THRESHOLD)
                {
                    lockoutUntil = DateTime.UtcNow.AddSeconds(LOCKOUT_SECONDS);
                }

                loginAttempts[username] = (attempts, lockoutUntil);
            }
            else
            {
                loginAttempts[username] = (1, DateTime.UtcNow);
            }
        }

        private (bool isLocked, DateTime lockoutUntil, int attempts) GetLockoutInfo(string username)
        {
            if (!loginAttempts.ContainsKey(username))
            {
                return (false, DateTime.UtcNow, 0);
            }

            var (attempts, lockoutUntil) = loginAttempts[username];

            if (attempts >= LOCKOUT_THRESHOLD && DateTime.UtcNow < lockoutUntil)
            {
                return (true, lockoutUntil, attempts);
            }

            return (false, lockoutUntil, attempts);
        }

        private void ClearExpiredLockouts(string username)
        {
            if (loginAttempts.ContainsKey(username))
            {
                var (attempts, lockoutUntil) = loginAttempts[username];
                if (DateTime.UtcNow >= lockoutUntil && attempts >= LOCKOUT_THRESHOLD)
                {
                    loginAttempts.Remove(username);
                }
            }
        }
    }
}