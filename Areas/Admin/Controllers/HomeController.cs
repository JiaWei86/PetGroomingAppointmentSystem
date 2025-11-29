using Microsoft.AspNetCore.Mvc;
using PetGroomingAppointmentSystem.Areas.Admin.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PetGroomingAppointmentSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class HomeController : Controller
    {
        // Temporary in-memory data (replace with actual database calls)
        private static List<Groomer> Groomers = new()
        {
            new Groomer { Id = 1, FirstName = "Anna", LastName = "Lee", Email = "anna.lee@petgroom.com", Specialty = "Dog Styling", Status = "Active" },
            new Groomer { Id = 2, FirstName = "Mark", LastName = "Chen", Email = "mark.chen@petgroom.com", Specialty = "Cat Grooming", Status = "Active" }
        };

        private static List<Appointment> Appointments = new()
        {
            new Appointment { Id = 1, GroomerId = 1, AppointmentDate = DateTime.Now.AddDays(1), StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(10, 0, 0), PetName = "Buddy", PetType = "Dog", ServiceType = "Full Groom", Status = "Confirmed" },
            new Appointment { Id = 2, GroomerId = 2, AppointmentDate = DateTime.Now.AddDays(1), StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(11, 30, 0), PetName = "Whiskers", PetType = "Cat", ServiceType = "Bath", Status = "Confirmed" },
            new Appointment { Id = 3, GroomerId = 1, AppointmentDate = DateTime.Now.AddDays(3), StartTime = new TimeSpan(2, 0, 0), EndTime = new TimeSpan(3, 0, 0), PetName = "Max", PetType = "Dog", ServiceType = "Nail Trim", Status = "Pending" },
            new Appointment { Id = 4, GroomerId = 2, AppointmentDate = DateTime.Now.AddDays(5), StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(12, 0, 0), PetName = "Mittens", PetType = "Cat", ServiceType = "Full Groom", Status = "Confirmed" }
        };

        // This is your Admin Dashboard
        public IActionResult Index()
        {
            ViewData["ActivePage"] = "Dashboard";
            
            // Get current month appointments
            var now = DateTime.Now;
            var appointmentsThisMonth = Appointments
                .Where(a => a.AppointmentDate.Year == now.Year && a.AppointmentDate.Month == now.Month)
                .ToList();

            ViewBag.Appointments = appointmentsThisMonth;
            ViewBag.Groomers = Groomers;
            ViewBag.CurrentMonth = now;

            return View(); // Looks for Views/Home/Index.cshtml
        }

        public IActionResult Groomer()
        {
            ViewData["ActivePage"] = "Groomer";
            return View();
        }

        // Action for the public-facing home page (if separate)
        public IActionResult ClientHome()
        {
            ViewData["ActivePage"] = "ClientHome";
            return View();
        }
        

        public IActionResult AboutUs()
        {
            ViewData["ActivePage"] = "AboutUs";
            return View();
        }

        public IActionResult EditClientHomePage()
        {
            ViewData["ActivePage"] = "EditClientHomePage";  
            return View();
        }

        // API endpoint for calendar navigation
        [HttpGet]
        public JsonResult GetAppointmentsByMonth(int year, int month)
        {
            var appointments = Appointments
                .Where(a => a.AppointmentDate.Year == year && a.AppointmentDate.Month == month)
                .Select(a => new
                {
                    id = a.Id,
                    date = a.AppointmentDate.ToString("yyyy-MM-dd"),
                    time = a.StartTime.ToString(@"hh\:mm"),
                    petName = a.PetName,
                    groomerName = Groomers.FirstOrDefault(g => g.Id == a.GroomerId)?.FirstName + " " + Groomers.FirstOrDefault(g => g.Id == a.GroomerId)?.LastName,
                    serviceType = a.ServiceType,
                    status = a.Status
                })
                .ToList();

            return Json(appointments);
        }

        public IActionResult Appointment()
        {
            ViewData["ActivePage"] = "Appointment";
            return View();
        }
    }
}