using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace PetGroomingAppointmentSystem.Areas.Admin.Controllers
{
    // ========================================
    // Admin Only Authorization Attribute
    // Only users with "admin" role can access
    // ========================================
    public class AdminOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var userRole = session.GetString("UserRole");
            var isAdminLoggedIn = session.GetString("IsAdminLoggedIn");

            // Check if user is logged in as admin
            if (isAdminLoggedIn != "true" || userRole != "admin")
            {
                context.Result = new RedirectToActionResult(
                    "Login",
                    "Auth",
                    new { area = "Admin" }
                );
            }

            base.OnActionExecuting(context);
        }
    }

    // ========================================
    // Staff Only Authorization Attribute
    // Only users with "staff" role can access
    // ========================================
    public class StaffOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var userRole = session.GetString("UserRole");
            var isStaffLoggedIn = session.GetString("IsStaffLoggedIn");

            // Check if user is logged in as staff
            if (isStaffLoggedIn != "true" || userRole != "staff")
            {
                context.Result = new RedirectToActionResult(
                    "Login",
                    "Auth",
                    new { area = "Admin" }
                );
            }

            base.OnActionExecuting(context);
        }
    }

    // ========================================
    // Authentication Controller
    // Handles login and logout for Admin and Staff
    // ========================================
    [Area("Admin")]
    public class AuthController : Controller
    {
        private readonly DB _dbContext;
        private readonly IPasswordService _passwordService;
        
        // Login attempt tracking (in-memory)
        private static Dictionary<string, (int attempts, DateTime lockoutUntil)> loginAttempts = new();
        private const int LOCKOUT_THRESHOLD = 3;
        private const int LOCKOUT_SECONDS = 15;

        public AuthController(DB dbContext, IPasswordService passwordService)
        {
            _dbContext = dbContext;
            _passwordService = passwordService;
        }

        // ========================================
        // GET: Login Page
        // ========================================
        public IActionResult Login()
        {
            // If already authenticated, redirect to appropriate home
            var userRole = HttpContext.Session.GetString("UserRole");
            var isAdminLoggedIn = HttpContext.Session.GetString("IsAdminLoggedIn");
            var isStaffLoggedIn = HttpContext.Session.GetString("IsStaffLoggedIn");

            if (isAdminLoggedIn == "true" && userRole == "admin")
            {
                return RedirectToAction("Index", "Home", new { area = "Admin" });
            }

            if (isStaffLoggedIn == "true" && userRole == "staff")
            {
                return RedirectToAction("Index", "Home", new { area = "Staff" });
            }

            return View();
        }

        // ========================================
        // POST: Login Authentication
        // ========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string username, string password)
        {
            // Validate input
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewData["Error"] = "Username and password are required.";
                return View();
            }

            // Clear expired lockouts
            ClearExpiredLockouts(username);

            // Check if account is locked
            var lockoutInfo = GetLockoutInfo(username);
            if (lockoutInfo.isLocked)
            {
                var remainingSeconds = (int)(lockoutInfo.lockoutUntil - DateTime.UtcNow).TotalSeconds;
                ViewData["IsLocked"] = true;
                ViewData["LockoutSeconds"] = Math.Max(0, remainingSeconds);
                ViewData["Error"] = $"Too many login attempts. Please try again in {remainingSeconds} seconds.";
                return View();
            }

            // ========================================
            // Try Admin Login First
            // ========================================
            var adminUser = _dbContext.Admins
                                    .FirstOrDefault(a => a.UserId == username || a.Name == username);

            if (adminUser != null && _passwordService.VerifyPassword(password, adminUser.Password))
            {
                // Clear failed login attempts
                loginAttempts.Remove(username);

                // Set Admin session variables
                HttpContext.Session.SetString("AdminUsername", adminUser.Name);
                HttpContext.Session.SetString("AdminId", adminUser.UserId);
                HttpContext.Session.SetString("UserRole", "admin");
                HttpContext.Session.SetString("IsAdminLoggedIn", "true");
                
                // Clear staff session if exists
                HttpContext.Session.Remove("IsStaffLoggedIn");
                HttpContext.Session.Remove("StaffId");

                return RedirectToAction("Index", "Home", new { area = "Admin" });
            }

            // ========================================
            // Try Staff Login - UPDATED VERSION
            // ========================================
            var staffUser = _dbContext.Staffs
               .FirstOrDefault(s => s.UserId == username || s.Name == username);
            
            if (staffUser != null && _passwordService.VerifyPassword(password, staffUser.Password))
            {
                // Clear failed login attempts
                loginAttempts.Remove(username);

                // Set Staff session variables
                HttpContext.Session.SetString("StaffUsername", staffUser.Name);
                HttpContext.Session.SetString("StaffId", staffUser.UserId);
                HttpContext.Session.SetString("UserRole", "staff");
                HttpContext.Session.SetString("IsStaffLoggedIn", "true");
                
                // Clear admin session if exists
                HttpContext.Session.Remove("IsAdminLoggedIn");
                HttpContext.Session.Remove("AdminId");

                return RedirectToAction("Index", "Home", new { area = "Staff" });
            }

            // ========================================
            // Login Failed
            // ========================================
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
                ViewData["Error"] = $"Invalid username or password. {remainingAttempts} attempt(s) remaining.";
            }

            return View();
        }

        // ========================================
        // Admin Logout
        // ========================================
        public IActionResult AdminLogout() => Logout();

        public IActionResult StaffLogout() => Logout();

        // ========================================
        // Universal Logout (for both Admin and Staff)
        // ========================================
        private IActionResult Logout()

        {
            var userRole = HttpContext.Session.GetString("UserRole");
            // Clear all session data
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth", new { area = "Admin" });
        }

        // ========================================
        // Helper: Increment Login Attempts
        // ========================================
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

        // ========================================
        // Helper: Get Lockout Information
        // ========================================
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

        // ========================================
        // Helper: Clear Expired Lockouts
        // ========================================
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