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
                CustomerId = customer.UserId,  // Changed from CustomerId to UserId
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

        [HttpGet]
        public IActionResult GetCustomerPets()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { success = false, message = "User not logged in" });

                // Get customer by UserId
                var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
                if (customer == null)
                    return NotFound(new { success = false, message = "Customer not found" });

                // Use UserId as CustomerId since Customer inherits from User
                var pets = _db.Pets
                    .Where(p => p.CustomerId == userId)  // Changed: use userId directly
                    .Select(p => new
                    {
                        id = p.PetId,
                        name = p.Name,
                        type = p.Type,
                        photo = p.Photo
                    })
                    .ToList();

                return Json(new { success = true, pets = pets });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetServices()
        {
            var dogServices = _db.Services
                .Where(s => s.Category.Name.ToLower() == "dog")
                .Select(s => new 
                {
                    serviceId = s.ServiceId,
                    name = s.Name,
                    price = s.Price,
                    durationTime = s.DurationTime
                })
                .ToList();

            var catServices = _db.Services
                .Where(s => s.Category.Name.ToLower() == "cat")
                .Select(s => new 
                {
                    serviceId = s.ServiceId,
                    name = s.Name,
                    price = s.Price,
                    durationTime = s.DurationTime
                })
                .ToList();

            return Json(new { dogServices, catServices });
        }

        [HttpPost]
        public IActionResult SaveAppointment([FromBody] AppointmentRequest request)
        {
            if (request == null || !ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid request data" });

            try
            {
                // Get current user ID from claims
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { success = false, message = "User not logged in" });

                // Get customer by UserId
                var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
                if (customer == null)
                    return Unauthorized(new { success = false, message = "Customer not found" });

                // Parse appointment date and time
                if (!DateTime.TryParse(request.Date + " " + request.Time, out var appointmentDateTime))
                    return BadRequest(new { success = false, message = "Invalid date or time" });

                // Verify service exists
                var service = _db.Services.FirstOrDefault(s => s.ServiceId == request.ServiceId);
                if (service == null)
                    return BadRequest(new { success = false, message = "Service not found" });

                // Save appointment for each selected pet
                var appointmentIds = new List<string>();
                
                foreach (var petId in request.PetIds)
                {
                    // Verify pet belongs to customer (use userId as CustomerId)
                    var pet = _db.Pets.FirstOrDefault(p => p.PetId == petId && p.CustomerId == userId);
                    if (pet == null)
                        return BadRequest(new { success = false, message = $"Pet {petId} not found or doesn't belong to you" });

                    // Create appointment
                    var appointment = new Appointment
                    {
                        AppointmentId = GenerateAppointmentId(),
                        CustomerId = userId,  // Changed: use userId directly
                        PetId = petId,
                        ServiceId = request.ServiceId,
                        AppointmentDateTime = appointmentDateTime,
                        DurationTime = service.DurationTime,
                        SpecialRequest = request.Notes,
                        Status = "Pending",
                        CreatedAt = DateTime.Now
                    };

                    _db.Appointments.Add(appointment);
                    appointmentIds.Add(appointment.AppointmentId);
                }

                _db.SaveChanges();

                return Ok(new 
                { 
                    success = true, 
                    message = "Appointment(s) booked successfully!",
                    appointmentIds = appointmentIds
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Error saving appointment: {ex.Message}" });
            }
        }

        private string GenerateAppointmentId()
        {
            return "APT" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
        }
    }

    // Request model for appointment
    public class AppointmentRequest
    {
        public string Date { get; set; }
        public string Time { get; set; }
        public string ServiceId { get; set; }
        public List<string> PetIds { get; set; } = new();
        public string Groomer { get; set; }
        public string Notes { get; set; }
    }
}