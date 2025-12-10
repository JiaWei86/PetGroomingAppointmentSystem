using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Areas.Customer.ViewModels;
using System.Security.Claims;

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
            // Get current user's ID from claims
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var loyaltyPoints = 0;

            // If user is logged in, fetch their loyalty points
            if (!string.IsNullOrEmpty(userId))
            {
                var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
                if (customer != null)
                {
                    loyaltyPoints = customer.LoyaltyPoint;
                }
            }

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

                RedeemGifts = _db.RedeemGifts.ToList(),
                CustomerLoyaltyPoints = loyaltyPoints
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

        [HttpGet]
        public IActionResult RedeemGift(string giftId)
        {
            if (string.IsNullOrEmpty(giftId))
                return BadRequest();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);

            if (customer == null)
                return Unauthorized();

            var gift = _db.RedeemGifts.FirstOrDefault(g => g.GiftId == giftId);

            if (gift == null)
                return NotFound();

            return Json(new
            {
                giftId = gift.GiftId,
                name = gift.Name,
                cost = gift.LoyaltyPointCost,
                maxQty = gift.Quantity,
                customerPoints = customer.LoyaltyPoint
            });
        }

        [HttpPost]
        public IActionResult RedeemGiftConfirm(string giftId, int quantity)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);

            if (customer == null)
                return Unauthorized();

            var gift = _db.RedeemGifts.FirstOrDefault(g => g.GiftId == giftId);
            if (gift == null)
                return NotFound();

            int totalCost = quantity * gift.LoyaltyPointCost;

            // Validation
            if (quantity <= 0)
                return BadRequest("Invalid quantity.");

            if (quantity > gift.Quantity)
                return BadRequest("Not enough gift stock.");

            if (totalCost > customer.LoyaltyPoint)
                return BadRequest("Not enough loyalty points.");

            // Update DB
            gift.Quantity -= quantity;
            customer.LoyaltyPoint -= totalCost;

            var redeemed = new CustomerRedeemGift
            {
                CrgId = Guid.NewGuid().ToString("N").Substring(0, 15),
                CustomerId = customer.CustomerId,
                GiftId = giftId,
                QuantityRedeemed = quantity,
                RedeemDate = DateTime.Now
            };

            _db.CustomerRedeemGifts.Add(redeemed);
            _db.SaveChanges();

            return Ok(new
            {
                success = true,
                message = "Gift redeemed successfully!"
            });
        }

    }
}