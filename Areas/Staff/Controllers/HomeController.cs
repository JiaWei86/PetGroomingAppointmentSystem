using Microsoft.AspNetCore.Mvc;
using PetGroomingAppointmentSystem.Areas.Admin.Controllers;

namespace PetGroomingAppointmentSystem.Areas.Staff.Controllers
{
    [Area("Staff")]
    [StaffOnly]
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