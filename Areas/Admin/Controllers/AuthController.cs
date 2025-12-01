using Microsoft.AspNetCore.Mvc;

namespace PetGroomingAppointmentSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuthController : Controller
    {
        private static Dictionary<string, (int attempts, DateTime lockoutUntil)> loginAttempts = new();
        private const int LOCKOUT_THRESHOLD = 3;
        private const int LOCKOUT_SECONDS = 15;

        // Hardcoded admin credentials
        private static readonly Dictionary<string, string> AdminCredentials = new()
        {
            { "Admin", "admin123" }
        };

        // Mock staff/groomer data - replace with database query in production
        private static readonly List<(string staffId, string icNumber)> StaffCredentials = new()
        {
            ("S001", "123456789012"),
            ("S002", "987654321098"),
            ("S003", "111222333444")
        };

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

            bool isValidAdmin = ValidateAdmin(username, password);
            bool isValidStaff = ValidateStaff(username, password);

            if (isValidAdmin || isValidStaff)
            {
                // Clear login attempts on successful login
                loginAttempts.Remove(username);

                // Set session variables
                HttpContext.Session.SetString("AdminUsername", username);
                HttpContext.Session.SetString("AdminRole", isValidAdmin ? "Admin" : "Groomer");
                HttpContext.Session.SetString("IsAdminLoggedIn", "true");

                return RedirectToAction("Index", "Home");
            }

            // Increment failed attempts
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
            HttpContext.Session.Remove("AdminRole");
            HttpContext.Session.Remove("IsAdminLoggedIn");
            return RedirectToAction("Login");
        }

        private bool ValidateAdmin(string username, string password)
        {
            return AdminCredentials.TryGetValue(username, out var storedPassword) && storedPassword == password;
        }

        private bool ValidateStaff(string username, string password)
        {
            return StaffCredentials.Any(staff => staff.staffId == username && staff.icNumber == password);
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