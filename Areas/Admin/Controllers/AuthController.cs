using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PetGroomingAppointmentSystem.Models;
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
        
        // Login attempt tracking (in-memory)
        private static Dictionary<string, (int attempts, DateTime lockoutUntil)> loginAttempts = new();
        private const int LOCKOUT_THRESHOLD = 3;
        private const int LOCKOUT_SECONDS = 15;

        public AuthController(DB dbContext)
        {
            _dbContext = dbContext;
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
                .FirstOrDefault(a => (a.Name == username || a.UserId == username)
                                    && a.Password == password
                                    && a.Role == "admin");

            if (adminUser != null)
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
            // First, find the staff record
            var staffUser = _dbContext.Staffs
                .FirstOrDefault(s => s.UserId == username 
                                    && s.Password == password 
                                    && s.Role == "staff");

            if (staffUser != null)
            {
                // Get the name from Users table (because Staff inherits from User)
                var userInfo = _dbContext.Users
                    .FirstOrDefault(u => u.UserId == staffUser.UserId);

                // Clear failed login attempts
                loginAttempts.Remove(username);

                // Set Staff session variables
                HttpContext.Session.SetString("StaffUsername", userInfo?.Name ?? "Staff User");
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
        public IActionResult AdminLogout()
        {
            // Clear all admin session data
            HttpContext.Session.Remove("AdminUsername");
            HttpContext.Session.Remove("AdminId");
            HttpContext.Session.Remove("UserRole");
            HttpContext.Session.Remove("IsAdminLoggedIn");
            
            // Clear general session
            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Auth", new { area = "Admin" });
        }

        // ========================================
        // Staff Logout
        // ========================================
        public IActionResult StaffLogout()
        {
            // Clear all staff session data
            HttpContext.Session.Remove("StaffUsername");
            HttpContext.Session.Remove("StaffId");
            HttpContext.Session.Remove("UserRole");
            HttpContext.Session.Remove("IsStaffLoggedIn");
            
            // Clear general session
            HttpContext.Session.Clear();

            return RedirectToAction("Login", "Auth", new { area = "Admin" });
        }

        // ========================================
        // Universal Logout (for both Admin and Staff)
        // ========================================
        public IActionResult Logout()
        {
            var userRole = HttpContext.Session.GetString("UserRole");

            // Clear all session data
            HttpContext.Session.Clear();

            // Redirect based on previous role
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