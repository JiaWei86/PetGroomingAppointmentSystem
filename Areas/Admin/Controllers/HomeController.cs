using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace PetGroomingAppointmentSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class HomeController : Controller
    {
        // ========== DASHBOARD ==========
        public IActionResult Index()
        {
            ViewData["ActivePage"] = "Dashboard";
            return View();
        }

        // ========== GROOMER ==========
        public IActionResult Groomer()
        {
            ViewData["ActivePage"] = "Groomer";
            return View();
        }
        public IActionResult Appointment()
        {
            ViewData["ActivePage"] = "Appointment";
            return View();
        }

        // ========== CUSTOMER ==========
        public IActionResult Customer()
        {
            ViewData["ActivePage"] = "Customer";
           
            return View();
        }

        // ========== PET ==========
        public IActionResult Pet()
        {
            ViewData["ActivePage"] = "Pet";
          
            return View();
        }

        // ========== LOYALTY POINT ==========
        public IActionResult LoyaltyPoint()
        {
            ViewData["ActivePage"] = "LoyaltyPoint";
           
            return View();
        }

        public IActionResult Service()
        {
            ViewData["ActivePage"] = "Service";
            return View();
        }

        public IActionResult AboutUs()
        {
            ViewData["ActivePage"] = "AboutUs";
            return View();
        }

        // ========== OTHER ACTIONS ==========
        public IActionResult customerIndex()
        {
            ViewData["ActivePage"] = "customerIndex";
            return View();
        }

        public IActionResult reports()
        {
            ViewData["ActivePage"] = "reports";
            return View();
        }





    }
}