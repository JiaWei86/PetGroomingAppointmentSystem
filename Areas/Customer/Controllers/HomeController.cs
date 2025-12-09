using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Areas.Customer.ViewModels;

namespace PetGroomingAppointmentSystem.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly DB _db;

        public HomeController(DB db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            var vm = new HomeViewModel
            {
                DogServices = _db.Services
                    .Include(s => s.Category)
                    .Where(s => s.Category.Name == "Dog")   // ⚠ category name must match DB
                    .ToList(),

                CatServices = _db.Services
                    .Include(s => s.Category)
                    .Where(s => s.Category.Name == "Cat")
                    .ToList(),

                RedeemGifts = _db.RedeemGifts.ToList()
            };

            return View(vm);
        }

        public IActionResult Profile()
        {
            return View();
        }

        public IActionResult About()
        {
            var staff = _db.Staffs.ToList();
            return View(staff);
        }

        public IActionResult Appointment()
        {
            return View();
        }

        public IActionResult History()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }
    }
}