using Microsoft.AspNetCore.Mvc;

namespace PetGroomingAppointmentSystem.Areas.Staff.Controllers
{
    [Area("Staff")]
    public class HomeController : Controller
    {

        public IActionResult Index()
        {
            ViewData["ActivePage"] = "Appointment"; 
            return View();
        }

        public IActionResult ApplyLeave()
        {
            ViewData["ActivePage"] = "ApplyLeave";
            return View();
        }

    }
}