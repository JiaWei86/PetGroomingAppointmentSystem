using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Areas.Customer.ViewModels;
using System.Security.Claims;
using System.Text;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace PetGroomingAppointmentSystem.Areas.Customer.Controllers;

[Area("Customer")]
public class HomeController : Controller
{
    private readonly DB _db;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public HomeController(DB db, IWebHostEnvironment webHostEnvironment)
    {
        _db = db;
        _webHostEnvironment = webHostEnvironment;
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
                    .Include(s => s.ServiceServiceCategories).ThenInclude(ssc => ssc.Category)
                    .Where(s => s.ServiceServiceCategories.Any(ssc => ssc.Category.Name == "Dog"))
                    .ToList(),

                CatServices = _db.Services
                    .Include(s => s.ServiceServiceCategories).ThenInclude(ssc => ssc.Category)
                    .Where(s => s.ServiceServiceCategories.Any(ssc => ssc.Category.Name == "Cat"))
                    .ToList(),

            RedeemGifts = _db.RedeemGifts.ToList(),
            CustomerLoyaltyPoints = loyaltyPoints
        };

        return View(vm);
    }

    public IActionResult Profile()
    {
        // Get current logged-in customer from SESSION
        var userId = HttpContext.Session.GetString("CustomerId");
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login", "Auth");
        }

        var customer = _db.Customers
            .Include(c => c.Redeems)
            .FirstOrDefault(c => c.UserId == userId);

        if (customer == null)
        {
            return NotFound("Customer not found");
        }

        // ✅ Use userId instead of customer.CustomerId
        var pets = _db.Pets.Where(p => p.CustomerId == userId).ToList();

        // Pass customer data to view
        ViewBag.Customer = customer;
        ViewBag.Pets = pets;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProfile(string name, string email, string ic, string phone)
    {
        try
        {
            var userId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer == null)
                return NotFound("Customer not found");

            // Update customer information
            customer.Name = name;
            customer.Email = email;
            customer.IC = ic;
            customer.Phone = phone;

            // Handle photo upload
            if (Request.Form.Files.Count > 0)
            {
                var photoFile = Request.Form.Files[0];
                if (photoFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "customers");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + photoFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await photoFile.CopyToAsync(fileStream);
                    }

                    customer.Photo = "/uploads/customers/" + uniqueFileName;
                }
            }

            _db.Customers.Update(customer);
            _db.SaveChanges();

            // Update session name if changed
            HttpContext.Session.SetString("CustomerName", customer.Name);

            return Json(new { success = true, message = "Profile updated successfully!" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult ChangePassword(string currentPassword, string newPassword)
    {
        try
        {
            var userId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer == null)
                return NotFound("Customer not found");

            // Verify current password (plain text comparison)
            if (customer.Password != currentPassword)
                return BadRequest(new { success = false, message = "Current password is incorrect" });

            // Update to new password (plain text)
            customer.Password = newPassword;
            _db.Customers.Update(customer);
            _db.SaveChanges();

            return Json(new { success = true, message = "Password changed successfully!" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetPets()
    {
        try
        {
            var userId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // ✅ Use UserId instead of CustomerId
            var pets = _db.Pets.Where(p => p.CustomerId == userId).ToList();
            return Json(new { success = true, data = pets });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddPet(string name, string type, string breed, int? age, string remark)
    {
        try
        {
            var userId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var pet = new Pet
            {
                PetId = "P" + Guid.NewGuid().ToString("N").Substring(0, 9).ToUpper(),
                Name = name,
                Type = type,
                Breed = breed,
                Age = age,
                Remark = remark,
                CustomerId = userId  // ✅ Use userId directly, not customer.CustomerId
            };

            // Handle pet photo upload
            if (Request.Form.Files.Count > 0)
            {
                var photoFile = Request.Form.Files[0];
                if (photoFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "pets");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + photoFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await photoFile.CopyToAsync(fileStream);
                    }

                    pet.Photo = "/uploads/pets/" + uniqueFileName;
                }
            }

            _db.Pets.Add(pet);
            _db.SaveChanges();

            return Json(new { success = true, message = "Pet added successfully!", data = pet });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePet(string petId, string name, string type, string breed, int? age, string remark)
    {
        try
        {
            var userId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // ✅ Use userId instead of customer.CustomerId
            var pet = _db.Pets.FirstOrDefault(p => p.PetId == petId && p.CustomerId == userId);
            if (pet == null)
                return NotFound("Pet not found");

            pet.Name = name;
            pet.Type = type;
            pet.Breed = breed;
            pet.Age = age;
            pet.Remark = remark;

            // Handle pet photo upload
            if (Request.Form.Files.Count > 0)
            {
                var photoFile = Request.Form.Files[0];
                if (photoFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "pets");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + photoFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await photoFile.CopyToAsync(fileStream);
                    }

                    pet.Photo = "/uploads/pets/" + uniqueFileName;
                }
            }

            _db.Pets.Update(pet);
            _db.SaveChanges();

            return Json(new { success = true, message = "Pet updated successfully!", data = pet });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult DeletePet(string petId)
    {
        try
        {
            var userId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // ✅ Use userId instead of customer.CustomerId
            var pet = _db.Pets.FirstOrDefault(p => p.PetId == petId && p.CustomerId == userId);
            if (pet == null)
                return NotFound("Pet not found");

            _db.Pets.Remove(pet);
            _db.SaveChanges();

            return Json(new { success = true, message = "Pet deleted successfully!" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetPetById(string petId)
    {
        try
        {
            var userId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var pet = _db.Pets.FirstOrDefault(p => p.PetId == petId && p.CustomerId == userId);
            if (pet == null)
                return NotFound("Pet not found");

            return Json(new { success = true, data = pet });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // Add this action to your HomeController

    [HttpPost]
    public async Task<IActionResult> UpdateProfileMultiPhoto(
        string name,
        string email,
        string ic,
        string phone,
        string allPhotosData)
    {
        try
        {
            var userId = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Not logged in" });

            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer == null)
                return Json(new { success = false, message = "Customer not found" });

            // Update basic info
            customer.Name = name;
            customer.Email = email;
            customer.IC = ic;
            customer.Phone = phone;

            // Process photos from JSON array
            var photoPaths = new List<string>();

            if (!string.IsNullOrEmpty(allPhotosData))
            {
                try
                {
                    var photoDataArray = System.Text.Json.JsonSerializer.Deserialize<List<string>>(allPhotosData);

                    if (photoDataArray != null)
                    {
                        for (int i = 0; i < photoDataArray.Count; i++)
                        {
                            var photoData = photoDataArray[i];

                            if (string.IsNullOrEmpty(photoData))
                                continue;

                            // If it's a base64 image (new upload), save it
                            if (photoData.StartsWith("data:image"))
                            {
                                try
                                {
                                    var base64Data = photoData.Substring(photoData.IndexOf(",") + 1);
                                    var imageBytes = Convert.FromBase64String(base64Data);

                                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "customers");
                                    if (!Directory.Exists(uploadsFolder))
                                        Directory.CreateDirectory(uploadsFolder);

                                    var uniqueFileName = $"{userId}_photo{i}_{Guid.NewGuid():N}.jpg";
                                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                                    photoPaths.Add($"/uploads/customers/{uniqueFileName}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error saving photo {i}: {ex.Message}");
                                }
                            }
                            // If it's already a path (existing photo), keep it
                            else if (photoData.StartsWith("/"))
                            {
                                photoPaths.Add(photoData);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing photos JSON: {ex.Message}");
                }
            }

            // Store as comma-separated paths (no limit!)
            customer.Photo = photoPaths.Any() ? string.Join(",", photoPaths) : null;

            _db.Customers.Update(customer);
            await _db.SaveChangesAsync();

            HttpContext.Session.SetString("CustomerName", customer.Name);

            return Json(new
            {
                success = true,
                message = "Profile updated successfully!",
                photoCount = photoPaths.Count
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
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
            // ✅ UPDATED: Try multiple ways to get userId
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // If not found in claims, check session
            if (string.IsNullOrEmpty(userId))
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            // Debug log
            Console.WriteLine($"GetCustomerPets - UserId from claims: {User.FindFirst(ClaimTypes.NameIdentifier)?.Value}");
            Console.WriteLine($"GetCustomerPets - UserId from session: {HttpContext.Session.GetString("CustomerId")}");
            Console.WriteLine($"GetCustomerPets - Final userId: {userId}");

            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not logged in" });
            }

            // Get customer by UserId
            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer == null)
            {
                Console.WriteLine($"GetCustomerPets - Customer not found for userId: {userId}");
                return Json(new { success = false, message = "Customer not found" });
            }

            // Get all pets for this customer
            var pets = _db.Pets
                .Where(p => p.CustomerId == userId)
                .Select(p => new
                {
                    id = p.PetId,
                    name = p.Name,
                    type = p.Type,
                    photo = p.Photo
                })
                .ToList();

            Console.WriteLine($"GetCustomerPets - Found {pets.Count} pets for userId: {userId}");

            foreach (var pet in pets)
            {
                Console.WriteLine($"  Pet: {pet.id} - {pet.name} ({pet.type})");
            }

            return Json(new { success = true, pets = pets });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetCustomerPets - Error: {ex.Message}");
            Console.WriteLine($"GetCustomerPets - StackTrace: {ex.StackTrace}");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetServices()
    {
        try
        {
            // ✅ UPDATED: Check both claims and session
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            Console.WriteLine($"GetServices - UserId: {userId}");

            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not logged in" });
            }

            var dogServices = _db.Services
                .Include(s => s.ServiceServiceCategories).ThenInclude(ssc => ssc.Category)
                .Where(s => s.ServiceServiceCategories.Any(ssc => ssc.Category.Name.ToLower() == "dog"))
                .Select(s => new 
                {
                    serviceId = s.ServiceId,
                    name = s.Name,
                    durationTime = s.DurationTime
                })
                .ToList();

            var catServices = _db.Services
                .Include(s => s.ServiceServiceCategories).ThenInclude(ssc => ssc.Category)
                .Where(s => s.ServiceServiceCategories.Any(ssc => ssc.Category.Name.ToLower() == "cat"))
                .Select(s => new 
                {
                    serviceId = s.ServiceId,
                    name = s.Name,
                    durationTime = s.DurationTime
                })
                .ToList();
 
            return Json(new { dogServices, catServices });
        }

    [HttpPost]
    public IActionResult SaveAppointment([FromBody] AppointmentRequest request)
    {
        try
        {
            // Validate request
            if (request == null)
                return BadRequest(new { success = false, message = "Invalid request data" });

            if (string.IsNullOrEmpty(request.Date) || string.IsNullOrEmpty(request.Time))
                return BadRequest(new { success = false, message = "Date and time are required" });

            if (string.IsNullOrEmpty(request.ServiceId))
                return BadRequest(new { success = false, message = "Service selection is required" });

            if (request.PetIds == null || request.PetIds.Count == 0)
                return BadRequest(new { success = false, message = "Please select at least one pet" });

            // Get current user ID
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not logged in" });

            // Verify customer exists
            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer == null)
                return Unauthorized(new { success = false, message = "Customer not found" });

            // Parse appointment date and time
            if (!DateTime.TryParse(request.Date + " " + request.Time, out var appointmentDateTime))
                return BadRequest(new { success = false, message = "Invalid date or time format" });

            // Verify appointment is in the future
            if (appointmentDateTime <= DateTime.Now)
                return BadRequest(new { success = false, message = "Appointment date must be in the future" });

            // Verify service exists
            var service = _db.Services.FirstOrDefault(s => s.ServiceId == request.ServiceId);
            if (service == null)
                return BadRequest(new { success = false, message = "Selected service not found" });

            // Get staff member (groomer) - assign the first available one if not specified
            Models.Staff assignedStaff = null;
            if (!string.IsNullOrEmpty(request.Groomer))
            {
                assignedStaff = _db.Staffs.FirstOrDefault(s => s.UserId == request.Groomer);
            }
            else
            {
                // Assign the first available staff member
                assignedStaff = _db.Staffs.FirstOrDefault(s => s.Role == "staff");
            }

                // Save appointment for each selected pet
                var appointmentIds = new List<string>();
                
                foreach (var petId in request.PetIds)
                {
                    // Verify pet belongs to customer (use userId as CustomerId)
                    var pet = _db.Pets.FirstOrDefault(p => p.PetId == petId && p.CustomerId == userId);
                    if (pet == null)
                    {
                        errors.Add($"Pet {petId} not found or doesn't belong to you");
                        continue;
                    }

                    // Generate unique appointment ID
                    var appointmentId = GenerateAppointmentId();

                    // Create appointment entity
                    var appointment = new Appointment
                    {
                        AppointmentId = appointmentId,
                        CustomerId = userId,
                        PetId = petId,
                        ServiceId = request.ServiceId,
                        AppointmentDateTime = appointmentDateTime,
                        DurationTime = service.DurationTime,
                        SpecialRequest = request.Notes ?? string.Empty,
                        Status = "confirmed",
                        StaffId = assignedStaff?.UserId,  // ✅ Assign the groomer here
                        CreatedAt = DateTime.Now
                    };

                    // Add to database
                    _db.Appointments.Add(appointment);
                    _db.SaveChanges(); // Save each appointment immediately

                    appointmentIds.Add(appointmentId);
                }
                catch (Exception ex)
                {
                    errors.Add($"Error booking appointment for pet {petId}: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            if (appointmentIds.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Failed to create any appointments: " + string.Join("; ", errors)
                });
            }

            var message = appointmentIds.Count == request.PetIds.Count
                ? "All appointments booked successfully!"
                : $"Successfully booked {appointmentIds.Count} of {request.PetIds.Count} appointments. Errors: {string.Join("; ", errors)}";

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

    // GET: Appointment History Data
    [HttpGet]
    public IActionResult GetAppointmentHistory()
    {
        try
        {
            // ✅ UPDATED: Check both claims and session
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not logged in" });

            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer == null)
                return NotFound(new { success = false, message = "Customer not found" });

            var appointments = _db.Appointments
                .Include(a => a.Pet)
                .Include(a => a.Service)
                .Include(a => a.Staff)
                .Where(a => a.CustomerId == userId)
                .OrderByDescending(a => a.AppointmentDateTime)
                .ToList();

                var result = appointments.Select(a => new
                {
                    appointmentId = a.AppointmentId,
                    date = a.AppointmentDateTime?.ToString("MMM dd, yyyy"),
                    time = a.AppointmentDateTime?.ToString("hh:mm tt"),                         
                    petName = a.Pet?.Name,
                    petImage = a.Pet?.Photo,
                    groomerName = a.Staff?.Name ?? "Not assigned",
                    serviceName = a.Service?.Name,
                    status = a.Status,
                    durationTime = a.DurationTime
                }).ToList();

            return Json(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAppointmentHistory Error: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    // GET: Redeem History Data
    [HttpGet]
    public IActionResult GetRedeemHistory()
    {
        try
        {
            // ✅ UPDATED: Check both claims and session
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not logged in" });

            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer == null)
                return NotFound(new { success = false, message = "Customer not found" });

            var redeemHistory = _db.CustomerRedeemGifts
                .Include(cr => cr.Gift)
                .Where(cr => cr.CustomerId == userId)
                .OrderByDescending(cr => cr.RedeemDate)
                .ToList();

            var result = redeemHistory.Select(cr => new
            {
                crgId = cr.CrgId,
                date = cr.RedeemDate?.ToString("MMM dd, yyyy"),
                itemName = cr.Gift?.Name,
                pointsUsed = cr.Gift?.LoyaltyPointCost * cr.QuantityRedeemed,
                quantity = cr.QuantityRedeemed,
                redeemedBy = customer.Name,
                status = "USED",
                giftImage = cr.Gift?.Photo
            }).ToList();

            return Json(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetRedeemHistory Error: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    // GET: History Summary (counts)
    [HttpGet]
    public IActionResult GetHistorySummary()
    {
        try
        {
            // ✅ UPDATED: Check both claims and session
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not logged in" });

            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer == null)
                return NotFound(new { success = false, message = "Customer not found" });

            var totalAppointments = _db.Appointments
                .Where(a => a.CustomerId == userId)
                .Count();

            var totalRedeemed = _db.CustomerRedeemGifts
                .Where(cr => cr.CustomerId == userId)
                .Count();

            return Json(new
            {
                success = true,
                totalAppointments = totalAppointments,
                totalRedeemed = totalRedeemed,
                loyaltyPoints = customer.LoyaltyPoint
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetHistorySummary Error: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    // GET: Appointment Details
    [HttpGet]
    public IActionResult GetAppointmentDetails(string appointmentId)
    {
        try
        {
            // ✅ UPDATED: Check both claims and session
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not logged in" });

            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer == null)
                return NotFound(new { success = false, message = "Customer not found" });

            var appointment = _db.Appointments
                .Include(a => a.Pet)
                .Include(a => a.Service)
                .Include(a => a.Staff)
                .FirstOrDefault(a => a.AppointmentId == appointmentId && a.CustomerId == userId);

            if (appointment == null)
                return NotFound(new { success = false, message = "Appointment not found" });

                var result = new
                {
                    appointmentId = appointment.AppointmentId,
                    date = appointment.AppointmentDateTime?.ToString("MMM dd, yyyy"),
                    time = appointment.AppointmentDateTime?.ToString("hh:mm tt"),
                    petNames = appointment.Pet?.Name,
                    groomerName = appointment.Staff?.Name ?? "Not assigned",
                    serviceName = appointment.Service?.Name,
                    amount = appointment.Service?.Price ?? 0,
                    status = appointment.Status,
                    notes = appointment.SpecialRequest,
                    durationTime = appointment.DurationTime
                };

            return Json(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

        // POST: Reschedule Appointment
        [HttpPost]
        public IActionResult RescheduleAppointment(string appointmentId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
                if (customer == null)
                    return NotFound();

                var appointment = _db.Appointments
                    .Include(a => a.Service)
                    .FirstOrDefault(a => a.AppointmentId == appointmentId && a.CustomerId == userId);

                if (appointment == null)
                    return NotFound();

                return Json(new
                {
                    success = true,
                    redirectUrl = $"/Customer/Home/Appointment?petId={appointment.PetId}&serviceId={appointment.ServiceId}&appointmentId={appointmentId}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Download Receipt as TXT
        [HttpGet]
        public IActionResult DownloadReceiptTxt(string appointmentId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
                if (customer == null)
                    return NotFound();

                var appointment = _db.Appointments
                    .Include(a => a.Pet)
                    .Include(a => a.Service)
                    .Include(a => a.Staff)
                    .FirstOrDefault(a => a.AppointmentId == appointmentId && a.CustomerId == userId);

                if (appointment == null)
                    return NotFound();

                var txtContent = GenerateReceiptTxt(appointment, customer);
                var fileName = $"Receipt_{appointmentId}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                
                return File(
                    Encoding.UTF8.GetBytes(txtContent),
                    "text/plain",
                    fileName
                );
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // GET: Download Receipt as PDF
        [HttpGet]
        public IActionResult DownloadReceiptPdf(string appointmentId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
                if (customer == null)
                    return NotFound();

                var appointment = _db.Appointments
                    .Include(a => a.Pet)
                    .Include(a => a.Service)
                    .Include(a => a.Staff)
                    .FirstOrDefault(a => a.AppointmentId == appointmentId && a.CustomerId == userId);

                if (appointment == null)
                    return NotFound();

                var pdfBytes = GenerateReceiptPdf(appointment, customer);
                var fileName = $"Receipt_{appointmentId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Helper: Generate TXT Receipt
        private string GenerateReceiptTxt(Appointment appointment, User customer)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔════════════════════════════════════════╗");
            sb.AppendLine("║      PET GROOMING APPOINTMENT RECEIPT   ║");
            sb.AppendLine("╚════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"Receipt #:        {appointment.AppointmentId}");
            sb.AppendLine($"Date Issued:      {DateTime.Now:MMM dd, yyyy HH:mm tt}");
            sb.AppendLine();
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine("CUSTOMER INFORMATION");
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine($"Name:             {customer.Name}");
            sb.AppendLine($"Email:            {customer.Email}");
            sb.AppendLine($"Phone:            {customer.Phone}");
            sb.AppendLine();
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine("APPOINTMENT DETAILS");
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine($"Date:             {appointment.AppointmentDateTime:MMM dd, yyyy}");
            sb.AppendLine($"Time:             {appointment.AppointmentDateTime:hh:mm tt}");
            sb.AppendLine($"Pet(s):           {appointment.Pet?.Name}");
            sb.AppendLine($"Groomer:          {appointment.Staff?.Name ?? "Not assigned"}");
            sb.AppendLine($"Service:          {appointment.Service?.Name}");
            sb.AppendLine($"Duration:         {appointment.DurationTime} minutes");
            sb.AppendLine($"Status:           {appointment.Status}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(appointment.SpecialRequest))
            {
                sb.AppendLine("────────────────────────────────────────");
                sb.AppendLine("SPECIAL REQUESTS");
                sb.AppendLine("────────────────────────────────────────");
                sb.AppendLine(appointment.SpecialRequest);
                sb.AppendLine();
            }
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine("AMOUNT");
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine($"Service Cost:     ${appointment.Service?.Price:F2}");
            sb.AppendLine($"Total Amount:     ${appointment.Service?.Price:F2}");
            sb.AppendLine();
            sb.AppendLine("════════════════════════════════════════");
            sb.AppendLine("Thank you for choosing our service!");
            sb.AppendLine("════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Printed: {DateTime.Now:MMM dd, yyyy HH:mm:ss}");

        return sb.ToString();
    }

    // Helper: Generate PDF Receipt
    private byte[] GenerateReceiptPdf(Appointment appointment, User customer)
    {
        using (var memoryStream = new MemoryStream())
        {
            var document = new Document(PageSize.A4, 40, 40, 40, 40);
            var writer = PdfWriter.GetInstance(document, memoryStream);
            document.Open();

            // Set fonts
            var titleFont = new Font(Font.FontFamily.HELVETICA, 18, Font.BOLD);
            var headerFont = new Font(Font.FontFamily.HELVETICA, 11, Font.BOLD);
            var normalFont = new Font(Font.FontFamily.HELVETICA, 10);
            var labelFont = new Font(Font.FontFamily.HELVETICA, 9, Font.BOLD);
            var smallFont = new Font(Font.FontFamily.HELVETICA, 8);

            // Title
            var title = new Paragraph("PET GROOMING APPOINTMENT RECEIPT", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter = 15;
            document.Add(title);

            // Receipt Info
            var receiptTable = new PdfPTable(2);
            receiptTable.SetWidths(new float[] { 50, 50 });
            receiptTable.DefaultCell.Border = 0;
            receiptTable.DefaultCell.Padding = 5;

            receiptTable.AddCell(new PdfPCell(new Phrase($"Receipt #: {appointment.AppointmentId}", normalFont)) { Border = 0 });
            receiptTable.AddCell(new PdfPCell(new Phrase($"Date Printed: {DateTime.Now:MMM dd, yyyy hh:mm tt}", normalFont)) { Border = 0, HorizontalAlignment = Element.ALIGN_RIGHT });

            document.Add(receiptTable);
            document.Add(new Paragraph(" "));

            // Divider line using Paragraph instead of LineSeparator
            var dividerParagraph = new Paragraph(new string('─', 50));
            dividerParagraph.Alignment = Element.ALIGN_CENTER;
            document.Add(dividerParagraph);
            document.Add(new Paragraph(" "));

                // Customer Section
                var customerHeader = new Paragraph("CUSTOMER INFORMATION", headerFont);
                document.Add(customerHeader);
                
                var customerTable = new PdfPTable(2);
                customerTable.SetWidths(new float[] { 30, 70 });
                customerTable.AddCell(new PdfPCell(new Phrase("Name:", normalFont)) { Border = 0 });
                customerTable.AddCell(new PdfPCell(new Phrase(customer.Name, normalFont)) { Border = 0 });
                customerTable.AddCell(new PdfPCell(new Phrase("Email:", normalFont)) { Border = 0 });
                customerTable.AddCell(new PdfPCell(new Phrase(customer.Email, normalFont)) { Border = 0 });
                customerTable.AddCell(new PdfPCell(new Phrase("Phone:", normalFont)) { Border = 0 });
                customerTable.AddCell(new PdfPCell(new Phrase(customer.Phone, normalFont)) { Border = 0 });

            document.Add(customerTable);
            document.Add(new Paragraph(" "));

            // Divider line
            document.Add(dividerParagraph);
            document.Add(new Paragraph(" "));

            // Appointment Section
            var appointmentHeader = new Paragraph("APPOINTMENT DETAILS", headerFont);
            appointmentHeader.SpacingAfter = 8;
            document.Add(appointmentHeader);

            var appointmentTable = new PdfPTable(2);
            appointmentTable.SetWidths(new float[] { 25, 75 });
            appointmentTable.DefaultCell.Border = 0;
            appointmentTable.DefaultCell.Padding = 4;

            appointmentTable.AddCell(new PdfPCell(new Phrase("Date:", labelFont)) { Border = 0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.AppointmentDateTime?.ToString("MMM dd, yyyy") ?? "N/A", normalFont)) { Border = 0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Time:", labelFont)) { Border = 0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.AppointmentDateTime?.ToString("hh:mm tt") ?? "N/A", normalFont)) { Border = 0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Pet(s):", labelFont)) { Border = 0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Pet?.Name ?? "N/A", normalFont)) { Border = 0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Groomer:", labelFont)) { Border = 0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Staff?.Name ?? "Not assigned", normalFont)) { Border = 0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Service:", labelFont)) { Border = 0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Service?.Name ?? "N/A", normalFont)) { Border = 0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Duration:", labelFont)) { Border = 0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase($"{appointment.DurationTime} minutes", normalFont)) { Border = 0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Status:", labelFont)) { Border = 0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Status, normalFont)) { Border = 0 });

            document.Add(appointmentTable);

            // Special Requests Section (if exists)
            if (!string.IsNullOrEmpty(appointment.SpecialRequest))
            {
                document.Add(new Paragraph(" "));
                document.Add(dividerParagraph);
                document.Add(new Paragraph(" "));

                var notesHeader = new Paragraph("SPECIAL REQUESTS", headerFont);
                notesHeader.SpacingAfter = 8;
                document.Add(notesHeader);

                var notes = new Paragraph(appointment.SpecialRequest, normalFont);
                notes.Alignment = Element.ALIGN_JUSTIFIED;
                document.Add(notes);
            }

                document.Add(space);

                // Amount Section
                var amountHeader = new Paragraph("AMOUNT", headerFont);
                document.Add(amountHeader);

                var amountTable = new PdfPTable(2);
                amountTable.SetWidths(new float[] { 70, 30 });
                amountTable.AddCell(new PdfPCell(new Phrase("Service Cost:", normalFont)) { Border = 0 });
                amountTable.AddCell(new PdfPCell(new Phrase($"${appointment.Service?.Price:F2}", normalFont)) { Border = 0, HorizontalAlignment = Element.ALIGN_RIGHT });
                amountTable.AddCell(new PdfPCell(new Phrase("Total Amount:", headerFont)) { Border = 0 });
                amountTable.AddCell(new PdfPCell(new Phrase($"${appointment.Service?.Price:F2}", headerFont)) { Border = 0, HorizontalAlignment = Element.ALIGN_RIGHT });

                document.Add(amountTable);
                document.Add(space);

            var footer = new Paragraph("Hope to see you next time ~ ^.^ ", smallFont);
            footer.Alignment = Element.ALIGN_CENTER;
            document.Add(footer);


            document.Close();
            return memoryStream.ToArray();
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