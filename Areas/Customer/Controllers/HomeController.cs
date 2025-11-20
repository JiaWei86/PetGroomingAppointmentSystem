using Microsoft.AspNetCore.Mvc;

namespace Demo123.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Profile()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Appointment()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }
    }
}