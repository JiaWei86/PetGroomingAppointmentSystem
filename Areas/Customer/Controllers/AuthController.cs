using Microsoft.AspNetCore.Mvc;

namespace PetGroomingAppointmentSystem.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class AuthController : Controller
    {
        // Mock database - replace with real database later
        private static List<Customer> customers = new()
        {
            new Customer { Id = 1, PhoneNumber = "0123456789", Name = "John Doe", IC = "123456789012", Email = "john@example.com", Password = "password123" }
        };

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

            var customer = customers.FirstOrDefault(c => c.PhoneNumber == phoneNumber && c.Password == password);

            if (customer == null)
            {
                ViewData["Error"] = "Invalid phone number or password.";
                return View();
            }

            // Store in session
            HttpContext.Session.SetString("CustomerId", customer.Id.ToString());
            HttpContext.Session.SetString("CustomerName", customer.Name);
            HttpContext.Session.SetString("CustomerPhone", customer.PhoneNumber);

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(Customer model)
        {
            if (string.IsNullOrEmpty(model.PhoneNumber) || string.IsNullOrEmpty(model.Password) || 
                string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.IC) || 
                string.IsNullOrEmpty(model.Email))
            {
                ViewData["Error"] = "All fields are required.";
                return View();
            }

            if (customers.Any(c => c.PhoneNumber == model.PhoneNumber))
            {
                ViewData["Error"] = "Phone number already registered.";
                return View();
            }

            model.Id = customers.Count + 1;
            customers.Add(model);

            ViewData["Success"] = "Registration successful! Please login.";
            return RedirectToAction("Login");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }

    public class Customer
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; }
        public string Name { get; set; }
        public string IC { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }
}