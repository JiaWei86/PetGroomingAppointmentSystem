using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetGroomingAppointmentSystem.Areas.Customer.ViewModels;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Services;
using System.IO;
using System.Security.Claims;
using System.Text;

namespace PetGroomingAppointmentSystem.Areas.Customer.Controllers;

[Area("Customer")]
public class HomeController : Controller
{
    private readonly DB _db;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IS3StorageService _s3Service;

    public HomeController(DB db, IWebHostEnvironment webHostEnvironment, IS3StorageService s3Service)
    {
        _db = db;
        _webHostEnvironment = webHostEnvironment;
        _s3Service = s3Service;
    }


    // ========== STATUS CHECK HELPER METHODS (Profile Only) ==========
    private Models.Customer? GetCurrentCustomerFromSession()
    {
        var userId = HttpContext.Session.GetString("CustomerId");
        if (string.IsNullOrEmpty(userId)) return null;
        return _db.Customers.FirstOrDefault(c => c.UserId == userId);
    }

    private bool IsCustomerBlocked(Models.Customer? customer)
    {
        return customer?.Status?.ToLower() == "blocked";
    }

    private IActionResult BlockedResponse(string action = "perform this action")
    {
        return Json(new {
            success = false,
            message = $"Your account is blocked. You cannot {action}.",
            statusCode = "BLOCKED"
        });
    }
    // =================================================================

    public IActionResult Index()
    {
        // Get current user's ID from SESSION (not Claims)
        var userId = HttpContext.Session.GetString("CustomerId");
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
                .Where(s => s.ServiceServiceCategories.Any(ssc => ssc.Category.PetType.ToLower() == "dog"))
                .ToList(),

            CatServices = _db.Services
                .Include(s => s.ServiceServiceCategories).ThenInclude(ssc => ssc.Category)
                .Where(s => s.ServiceServiceCategories.Any(ssc => ssc.Category.PetType.ToLower() == "cat"))
                .ToList(),

            RedeemGifts = _db.RedeemGifts.ToList(),
            CustomerLoyaltyPoints = loyaltyPoints
        };

        return View(vm);
    }

    public IActionResult Profile()
    {
        var customer = GetCurrentCustomerFromSession();
        if (customer == null)
            return Unauthorized();

        customer = _db.Customers
            .Include(c => c.Redeems)
            .FirstOrDefault(c => c.UserId == customer.UserId);

        if (customer == null)
            return NotFound("Customer not found");

        if (IsCustomerBlocked(customer))
            return BlockedResponse("view profile");

        var pets = _db.Pets.Where(p => p.CustomerId == customer.UserId).ToList();

        ViewBag.Customer = customer;
        ViewBag.Pets = pets;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProfile(string name, string email, string ic, string phone)
    {
        try
        {
            var customer = GetCurrentCustomerFromSession();
            if (customer == null)
                return Unauthorized();

            if (IsCustomerBlocked(customer))
                return BlockedResponse("update profile");

            customer.Name = name;
            customer.Email = email;
            customer.IC = ic;
            customer.Phone = phone;

            if (Request.Form.Files.Count >0)
            {
                var photoFile = Request.Form.Files[0];
                if (photoFile.Length >0)
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
            var customer = GetCurrentCustomerFromSession();
            if (customer == null)
                return Unauthorized();

            if (IsCustomerBlocked(customer))
                return BlockedResponse("change password");

            if (customer.Password != currentPassword)
                return BadRequest(new { success = false, message = "Current password is incorrect" });

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
            var customer = GetCurrentCustomerFromSession();
            if (customer == null)
                return Unauthorized();

            var pets = _db.Pets
                .Where(p => p.CustomerId == customer.UserId)
                .Select(p => new
                {
                    petId = p.PetId,
                    name = p.Name,
                    age = p.Age,
                    type = p.Type,
                    breed = p.Breed,
                    remark = p.Remark,
                    photo = p.Photo
                })
                .ToList();
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
            var customer = GetCurrentCustomerFromSession();
            if (customer == null)
                return Unauthorized();

            // ========== STATUS CHECK ==========
            if (IsCustomerBlocked(customer))
                return BlockedResponse("add pet");

            // Generate sequential PetId (P001, P002, etc.)
            int nextNumber = 1;

            var existingPetIds = _db.Pets
                .Where(p => p.PetId.StartsWith("P"))
                .Select(p => p.PetId)
                .ToList();

            if (existingPetIds.Any())
            {
                nextNumber = existingPetIds
                    .Select(id => int.TryParse(id.Substring(1), out int num) ? num : 0)
                    .Max() + 1;
            }

            var petId = $"P{nextNumber:D3}";

            var pet = new Pet
            {
                PetId = petId,
                Name = name,
                Type = type,
                Breed = breed,
                Age = age,
                Remark = remark,
                CustomerId = customer.UserId
            };

            // ✅ Upload photo to S3 instead of local storage
            if (Request.Form.Files.Count > 0)
            {
                var photoFile = Request.Form.Files[0];
                if (photoFile.Length > 0)
                {
                    try
                    {
                        // Convert file to base64 and upload to S3
                        using var memoryStream = new MemoryStream();
                        await photoFile.CopyToAsync(memoryStream);
                        var base64String = Convert.ToBase64String(memoryStream.ToArray());
                        var contentType = photoFile.ContentType ?? "image/jpeg";
                        var base64Image = $"data:{contentType};base64,{base64String}";

                        // Upload to S3 and get CloudFront URL
                        var cloudFrontUrl = await _s3Service.UploadBase64ImageAsync(
                            base64Image,
                            $"pets/{petId}"
                        );
                        pet.Photo = cloudFrontUrl;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error uploading pet photo to S3: {ex.Message}");
                        // Fallback: leave photo as null if S3 upload fails
                    }
                }
            }

            _db.Pets.Add(pet);
            await _db.SaveChangesAsync();

            var result = new
            {
                petId = pet.PetId,
                name = pet.Name,
                age = pet.Age,
                type = pet.Type,
                breed = pet.Breed,
                remark = pet.Remark,
                photo = pet.Photo,
                customerId = pet.CustomerId
            };

            return Json(new { success = true, message = "Pet added successfully!", data = result });
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
            var customer = GetCurrentCustomerFromSession();
            if (customer == null)
                return Unauthorized();

            if (IsCustomerBlocked(customer))
                return BlockedResponse("update pet");

            var pet = _db.Pets.FirstOrDefault(p => p.PetId == petId && p.CustomerId == customer.UserId);
            if (pet == null)
                return NotFound("Pet not found");

            pet.Name = name;
            pet.Type = type;
            pet.Breed = breed;
            pet.Age = age;
            pet.Remark = remark;

            // ✅ Upload new photo to S3 if provided
            if (Request.Form.Files.Count > 0)
            {
                var photoFile = Request.Form.Files[0];
                if (photoFile.Length > 0)
                {
                    try
                    {
                        // Convert file to base64 and upload to S3
                        using var memoryStream = new MemoryStream();
                        await photoFile.CopyToAsync(memoryStream);
                        var base64String = Convert.ToBase64String(memoryStream.ToArray());
                        var contentType = photoFile.ContentType ?? "image/jpeg";
                        var base64Image = $"data:{contentType};base64,{base64String}";

                        // Upload to S3 and get CloudFront URL
                        var cloudFrontUrl = await _s3Service.UploadBase64ImageAsync(
                            base64Image,
                            $"pets/{petId}"
                        );
                        pet.Photo = cloudFrontUrl;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error uploading pet photo to S3: {ex.Message}");
                        // Keep existing photo if S3 upload fails
                    }
                }
            }

            _db.Pets.Update(pet);
            await _db.SaveChangesAsync();

            var result = new
            {
                petId = pet.PetId,
                name = pet.Name,
                age = pet.Age,
                type = pet.Type,
                breed = pet.Breed,
                remark = pet.Remark,
                photo = pet.Photo,
                customerId = pet.CustomerId
            };

            return Json(new { success = true, message = "Pet updated successfully!", data = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult DeletePet([FromForm] string petId)
    {
        try
        {
            var customer = GetCurrentCustomerFromSession();
            if (customer == null)
            {
                Console.WriteLine($"DeletePet: Not logged in. petId={petId}");
                return Json(new { success = false, message = "Not logged in" });
            }

            if (IsCustomerBlocked(customer))
            {
                Console.WriteLine($"DeletePet: Customer blocked. customerId={customer.UserId}, petId={petId}");
                return BlockedResponse("delete pet");
            }

            Console.WriteLine($"DeletePet called. customerId={customer.UserId}, petId={petId}");

            var pet = _db.Pets.FirstOrDefault(p => p.PetId == petId && p.CustomerId == customer.UserId);
            if (pet == null)
            {
                Console.WriteLine($"DeletePet: Pet not found for customer. customerId={customer.UserId}, petId={petId}");
                return Json(new { success = false, message = "Pet not found" });
            }

            // Check for related appointments
            var hasAppointments = _db.Appointments.Any(a => a.PetId == petId);
            if (hasAppointments)
            {
                Console.WriteLine($"DeletePet: Pet has existing appointments. customerId={customer.UserId}, petId={petId}");
                return Json(new { success = false, message = "Cannot delete pet with existing appointments. Please cancel or complete them first." });
            }

            _db.Pets.Remove(pet);
            _db.SaveChanges();

            Console.WriteLine($"DeletePet: Pet removed. customerId={customer.UserId}, petId={petId}");

            return Json(new { success = true, message = "Pet deleted successfully!" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DeletePet Exception: {ex.Message}\n{ex.StackTrace}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetPetById(string petId)
    {
        try
        {
            var customer = GetCurrentCustomerFromSession();
            if (customer == null)
                return Json(new { success = false, message = "Not logged in" });

            var pet = _db.Pets
                .Where(p => p.PetId == petId && p.CustomerId == customer.UserId)
                .Select(p => new
                {
                    petId = p.PetId,
                    name = p.Name,
                    age = p.Age,
                    type = p.Type,
                    breed = p.Breed,
                    remark = p.Remark,
                    photo = p.Photo,
                    customerId = p.CustomerId
                })
                .FirstOrDefault();

            if (pet == null)
                return Json(new { success = false, message = "Pet not found" });

            return Json(new { success = true, data = pet });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

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
            var customer = GetCurrentCustomerFromSession();
            if (customer == null)
                return Json(new { success = false, message = "Not logged in" });

            if (IsCustomerBlocked(customer))
                return BlockedResponse("update profile");

            customer.Name = name;
            customer.Email = email;
            customer.IC = ic;
            customer.Phone = phone;

            var photoPaths = new List<string>();

            if (!string.IsNullOrEmpty(allPhotosData))
            {
                try
                {
                    var photoDataArray = System.Text.Json.JsonSerializer.Deserialize<List<string>>(allPhotosData);

                    if (photoDataArray != null)
                    {
                        for (int i =0; i < photoDataArray.Count; i++)
                        {
                            var photoData = photoDataArray[i];

                            if (string.IsNullOrEmpty(photoData))
                                continue;

                            if (photoData.StartsWith("data:image"))
                            {
                                try
                                {
                                    var cloudFrontUrl = await _s3Service.UploadBase64ImageAsync(
                                        photoData,
                                        $"customers/{customer.UserId}"
                                    );
                                    photoPaths.Add(cloudFrontUrl);
                                }

                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error uploading photo {i} to S3: {ex.Message}");
                                }
                            }
                            // Keep existing CloudFront URLs
                            else if (photoData.StartsWith("https://"))
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

        // Check both claims and session for user ID
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            userId = HttpContext.Session.GetString("CustomerId");
        }

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();


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
        // Check both claims and session for user ID
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            userId = HttpContext.Session.GetString("CustomerId");
        }

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, message = "User not logged in." });

        var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);

        if (customer == null)
            return NotFound(new { success = false, message = "Customer not found." });

        var gift = _db.RedeemGifts.FirstOrDefault(g => g.GiftId == giftId);
        if (gift == null)
            return NotFound(new { success = false, message = "Gift not found." });

        int totalCost = quantity * gift.LoyaltyPointCost;

        // Validation
        if (quantity <= 0)
            return BadRequest(new { success = false, message = "Invalid quantity." });

        if (quantity > gift.Quantity)
            return BadRequest(new { success = false, message = "Not enough gift stock." });

        if (totalCost > customer.LoyaltyPoint)
            return BadRequest(new { success = false, message = "Not enough loyalty points." });

        // Update DB
        gift.Quantity -= quantity;
        customer.LoyaltyPoint -= totalCost;

        var redeemed = new CustomerRedeemGift
        {
            CrgId = Guid.NewGuid().ToString("N").Substring(0, 15),
            CustomerId = customer.UserId,
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
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not logged in" });
            }

            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer == null)
            {
                return Json(new { success = false, message = "Customer not found" });
            }

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

            return Json(new { success = true, pets = pets });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetCustomerPets - Error: {ex.Message}");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetServices()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not logged in" });
            }

            var dogServices = _db.Services
                .Include(s => s.ServiceServiceCategories).ThenInclude(ssc => ssc.Category)
                .Where(s => s.ServiceServiceCategories.Any(ssc => ssc.Category.PetType.ToLower() == "dog"))
                .Select(s => new
                {
                    serviceId = s.ServiceId,
                    name = s.Name,
                    durationTime = s.DurationTime
                })
                .ToList();

            var catServices = _db.Services
                .Include(s => s.ServiceServiceCategories).ThenInclude(ssc => ssc.Category)
                .Where(s => s.ServiceServiceCategories.Any(ssc => ssc.Category.PetType.ToLower() == "cat"))
                .Select(s => new
                {
                    serviceId = s.ServiceId,
                    name = s.Name,
                    durationTime = s.DurationTime
                })
                .ToList();

            return Json(new { success = true, dogServices, catServices });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetServices - Error: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
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

            if (request.PetIds == null || request.PetIds.Count ==0)
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

            var appointmentIds = new List<string>();
            var errors = new List<string>();
            int totalPointsEarned = 0; // ✅ 追踪总积分

            foreach (var petId in request.PetIds)
            {
                try
                {
                    // Verify pet belongs to customer
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
                        Status = "Confirmed",
                        StaffId = assignedStaff?.UserId,  // ✅ Assign the groomer here
                        CreatedAt = DateTime.Now
                    };

                    // Add to database
                    _db.Appointments.Add(appointment);
                    _db.SaveChanges();

                    // ✅ ADD LOYALTY POINTS (10 points per pet booked)
                    customer.LoyaltyPoint += 10;
                    totalPointsEarned += 10; // Track total points for this booking session
                    _db.Customers.Update(customer);
                    _db.SaveChanges();

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
                ? $"All appointments booked successfully! You earned {totalPointsEarned} loyalty points."
                : $"Successfully booked {appointmentIds.Count} of {request.PetIds.Count} appointments. You earned {totalPointsEarned} loyalty points. Errors: {string.Join("; ", errors)}";

            return Ok(new
            {
                success = true,
                message = message,
                appointmentIds = appointmentIds,
                loyaltyPointsEarned = totalPointsEarned, // ✅ 返回获得的积分
                partialFailure = appointmentIds.Count < request.PetIds.Count
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SaveAppointment Exception: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            return BadRequest(new
            {
                success = false,
                message = $"Server error: {ex.InnerException?.Message ?? ex.Message}"
            });
        }
    }

    // GET: Appointment History Data
    [HttpGet]
    public IActionResult GetAppointmentHistory()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                userId = HttpContext.Session.GetString("CustomerId");

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
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                userId = HttpContext.Session.GetString("CustomerId");

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
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                userId = HttpContext.Session.GetString("CustomerId");

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
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                userId = HttpContext.Session.GetString("CustomerId");

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

    // Helper: Generate TXT Receipt
    private string GenerateReceiptTxt(Appointment appointment, User customer)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔════════════════════════════════════════╗");
        sb.AppendLine("║ PET GROOMING APPOINTMENT RECEIPT ║");
        sb.AppendLine("╚════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine($"Receipt #: {appointment.AppointmentId}");
        sb.AppendLine($"Date Issued: {DateTime.Now:MMM dd, yyyy HH:mm tt}");
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────────");
        sb.AppendLine("CUSTOMER INFORMATION");
        sb.AppendLine("────────────────────────────────────────");
        sb.AppendLine($"Name: {customer.Name}");
        sb.AppendLine($"Email: {customer.Email}");
        sb.AppendLine($"Phone: {customer.Phone}");
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────────");
        sb.AppendLine("APPOINTMENT DETAILS");
        sb.AppendLine("────────────────────────────────────────");
        sb.AppendLine($"Date: {appointment.AppointmentDateTime:MMM dd, yyyy}");
        sb.AppendLine($"Time: {appointment.AppointmentDateTime:hh:mm tt}");
        sb.AppendLine($"Pet(s): {appointment.Pet?.Name}");
        sb.AppendLine($"Groomer: {appointment.Staff?.Name ?? "Not assigned"}");
        sb.AppendLine($"Service: {appointment.Service?.Name}");
        sb.AppendLine($"Duration: {appointment.DurationTime} minutes");
        sb.AppendLine($"Status: {appointment.Status}");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(appointment.SpecialRequest))
        {
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine("SPECIAL REQUESTS");
            sb.AppendLine("────────────────────────────────────────");
            sb.AppendLine(appointment.SpecialRequest);
            sb.AppendLine();
        }
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
            var document = new Document(PageSize.A4,40,40,40,40);
            var writer = PdfWriter.GetInstance(document, memoryStream);
            document.Open();

            // Set fonts
            var titleFont = new Font(Font.FontFamily.HELVETICA,18, Font.BOLD);
            var headerFont = new Font(Font.FontFamily.HELVETICA,11, Font.BOLD);
            var normalFont = new Font(Font.FontFamily.HELVETICA,10);
            var labelFont = new Font(Font.FontFamily.HELVETICA,9, Font.BOLD);
            var smallFont = new Font(Font.FontFamily.HELVETICA,8);

            // Title
            var title = new Paragraph("PET GROOMING APPOINTMENT RECEIPT", titleFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter =15;
            document.Add(title);

            // Receipt Info
            var receiptTable = new PdfPTable(2);
            receiptTable.SetWidths(new float[] {50,50 });
            receiptTable.DefaultCell.Border =0;
            receiptTable.DefaultCell.Padding =5;

            receiptTable.AddCell(new PdfPCell(new Phrase($"Receipt #: {appointment.AppointmentId}", normalFont)) { Border =0 });
            receiptTable.AddCell(new PdfPCell(new Phrase($"Date Printed: {DateTime.Now:MMM dd, yyyy hh:mm tt}", normalFont)) { Border =0, HorizontalAlignment = Element.ALIGN_RIGHT });

            document.Add(receiptTable);
            document.Add(new Paragraph(" "));

            // Divider line using Paragraph instead of LineSeparator
            var dividerParagraph = new Paragraph(new string('─',50));
            dividerParagraph.Alignment = Element.ALIGN_CENTER;
            document.Add(dividerParagraph);
            document.Add(new Paragraph(" "));

            // Customer Section
            var customerHeader = new Paragraph("CUSTOMER INFORMATION", headerFont);
            customerHeader.SpacingAfter =8;
            document.Add(customerHeader);

            var customerTable = new PdfPTable(2);
            customerTable.SetWidths(new float[] {25,75 });
            customerTable.DefaultCell.Border =0;
            customerTable.DefaultCell.Padding =4;

            customerTable.AddCell(new PdfPCell(new Phrase("Name:", labelFont)) { Border =0 });
            customerTable.AddCell(new PdfPCell(new Phrase(customer.Name, normalFont)) { Border =0 });
            customerTable.AddCell(new PdfPCell(new Phrase("Email:", labelFont)) { Border =0 });
            customerTable.AddCell(new PdfPCell(new Phrase(customer.Email, normalFont)) { Border =0 });
            customerTable.AddCell(new PdfPCell(new Phrase("Phone:", labelFont)) { Border =0 });
            customerTable.AddCell(new PdfPCell(new Phrase(customer.Phone, normalFont)) { Border =0 });

            document.Add(customerTable);
            document.Add(new Paragraph(" "));

            // Divider line
            document.Add(dividerParagraph);
            document.Add(new Paragraph(" "));

            // Appointment Section
            var appointmentHeader = new Paragraph("APPOINTMENT DETAILS", headerFont);
            appointmentHeader.SpacingAfter =8;
            document.Add(appointmentHeader);

            var appointmentTable = new PdfPTable(2);
            appointmentTable.SetWidths(new float[] {25,75 });
            appointmentTable.DefaultCell.Border =0;
            appointmentTable.DefaultCell.Padding =4;

            appointmentTable.AddCell(new PdfPCell(new Phrase("Date:", labelFont)) { Border =0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.AppointmentDateTime?.ToString("MMM dd, yyyy") ?? "N/A", normalFont)) { Border =0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Time:", labelFont)) { Border =0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.AppointmentDateTime?.ToString("hh:mm tt") ?? "N/A", normalFont)) { Border =0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Pet(s):", labelFont)) { Border =0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Pet?.Name ?? "N/A", normalFont)) { Border =0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Groomer:", labelFont)) { Border =0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Staff?.Name ?? "Not assigned", normalFont)) { Border =0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Service:", labelFont)) { Border =0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Service?.Name ?? "N/A", normalFont)) { Border =0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Duration:", labelFont)) { Border =0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase($"{appointment.DurationTime} minutes", normalFont)) { Border =0 });

            appointmentTable.AddCell(new PdfPCell(new Phrase("Status:", labelFont)) { Border =0 });
            appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Status, normalFont)) { Border =0 });

            document.Add(appointmentTable);

            // Special Requests Section (if exists)
            if (!string.IsNullOrEmpty(appointment.SpecialRequest))
            {
                document.Add(new Paragraph(" "));
                document.Add(dividerParagraph);
                document.Add(new Paragraph(" "));

                var notesHeader = new Paragraph("SPECIAL REQUESTS", headerFont);
                notesHeader.SpacingAfter =8;
                document.Add(notesHeader);

                var notes = new Paragraph(appointment.SpecialRequest, normalFont);
                notes.Alignment = Element.ALIGN_JUSTIFIED;
                document.Add(notes);
            }

            // Footer
            document.Add(new Paragraph(" "));
            document.Add(dividerParagraph);
            document.Add(new Paragraph(" "));

            var footer1 = new Paragraph("Thank you for choosing our service!", smallFont);
            footer1.Alignment = Element.ALIGN_CENTER;
            document.Add(footer1);

            var footer = new Paragraph("Hope to see you next time ~ ^.^ ", smallFont);
            footer.Alignment = Element.ALIGN_CENTER;
            document.Add(footer);


            document.Close();
            return memoryStream.ToArray();
        }
    }

    // Update the GenerateAppointmentId method
    private string GenerateAppointmentId()
    {
        // Get the last appointment ID and increment it
        var lastAppointment = _db.Appointments
            .OrderByDescending(a => a.AppointmentId)
            .FirstOrDefault();

        int nextNumber =1;

        if (lastAppointment != null && lastAppointment.AppointmentId.StartsWith("AP"))
        {
            // Extract the number from the last ID (e.g., "AP001" -> 1)
            if (int.TryParse(lastAppointment.AppointmentId.Substring(2), out int lastNumber))
            {
                nextNumber = lastNumber +1;
            }
        }

        return $"AP{nextNumber:D3}"; // Formats as AP001, AP002, etc.
    }

    [HttpPost]
    public async Task<IActionResult> SavePet([FromBody] PetDto petDto)
    {
        try
        {
            var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                customerId = HttpContext.Session.GetString("CustomerId");
            }

            if (string.IsNullOrEmpty(customerId))
                return Json(new { success = false, message = "User not authenticated" });

            // Generate sequential pet ID (P001, P002, P003, etc.)
            var lastPet = _db.Pets
                .OrderByDescending(p => p.PetId)
                .FirstOrDefault();

            int nextNumber = 1;
            if (lastPet != null && lastPet.PetId.StartsWith("P"))
            {
                string numericPart = lastPet.PetId.Substring(1);
                if (int.TryParse(numericPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            var petId = $"P{nextNumber:D3}";
            string? photoPath = null;

            // ✅ Upload base64 photo to S3
            if (!string.IsNullOrEmpty(petDto.Photo) && petDto.Photo.StartsWith("data:image"))
            {
                try
                {
                    var cloudFrontUrl = await _s3Service.UploadBase64ImageAsync(
                        petDto.Photo,
                        $"pets/{petId}"
                    );
                    photoPath = cloudFrontUrl;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading pet photo to S3: {ex.Message}");
                }
            }
            // Keep existing CloudFront URL if provided
            else if (!string.IsNullOrEmpty(petDto.Photo) && petDto.Photo.StartsWith("https://"))
            {
                photoPath = petDto.Photo;
            }

            var pet = new Pet
            {
                PetId = petId,
                Name = petDto.Name,
                Type = petDto.Category,
                Photo = photoPath,
                CustomerId = customerId,
                Breed = petDto.Breed,
                Age = petDto.Age,
                Remark = petDto.Remark
            };

            _db.Pets.Add(pet);
            await _db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Pet saved successfully",
                petId = petId,
                photo = photoPath
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    public class PetDto
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Photo { get; set; }
        public int? Age { get; set; }
        public string Breed { get; set; }
        public string Remark { get; set; }
    }

    [HttpGet]
    public IActionResult CheckUserStatus()
    {
        // Check both claims-based and session-based authentication
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // If not found in claims, check session
        if (string.IsNullOrEmpty(userId))
        {
            userId = HttpContext.Session.GetString("CustomerId");
        }

        var isLoggedIn = !string.IsNullOrEmpty(userId);

        return Json(new { isLoggedIn = isLoggedIn });
    }

    [HttpGet]
    public IActionResult GetAvailableGroomers()
    {
        try
        {
            // Fetch all staff members from the Staffs table
            var groomers = _db.Staffs
                .Where(s => s.Role == "staff") // Ensure we only get staff
                .Select(s => new
                {
                    id = s.UserId,              // e.g., S001, S002, S003
                    name = s.Name,
                    position = s.Position,      // e.g., Senior Groomer, Junior Groomer
                })
                .ToList();

            return Json(new { success = true, groomers = groomers });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: Check if appointment can be cancelled (validate 24-hour rule)
    [HttpGet]
    public IActionResult CanCancelAppointment(string appointmentId)
    {
        try
        {
            var appointment = _db.Appointments.FirstOrDefault(a => a.AppointmentId == appointmentId);
            if (appointment == null)
                return Json(new { canCancel = false, message = "Appointment not found" });

            // Check status
            if (appointment.Status == "Cancelled" || appointment.Status == "Completed")
                return Json(new { canCancel = false, message = $"Cannot cancel a {appointment.Status} appointment" });

            // Check 24-hour rule
            if (appointment.AppointmentDateTime.HasValue)
            {
                var timeUntilAppointment = appointment.AppointmentDateTime.Value - DateTime.Now;

                if (timeUntilAppointment.TotalHours <24)
                {
                    var hoursRemaining = Math.Round(timeUntilAppointment.TotalHours,1);
                    return Json(new
                    {
                        canCancel = false,
                        message = $"Cannot cancel within24 hours. {hoursRemaining} hours remaining."
                    });
                }
            }

            return Json(new { canCancel = true, message = "Appointment can be cancelled" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CanCancelAppointment Error: {ex.Message}");
            return Json(new { canCancel = false, message = ex.Message });
        }
    }

    // POST: Cancel Appointment
    [HttpPost]
    public IActionResult CancelAppointment([FromBody] CancelRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { success = false, message = "User not logged in" });

            var appointment = _db.Appointments
                .FirstOrDefault(a => a.AppointmentId == request.AppointmentId && a.CustomerId == userId);

            if (appointment == null)
                return NotFound(new { success = false, message = "Appointment not found" });

            // Check if already cancelled
            if (appointment.Status == "Cancelled")
                return BadRequest(new { success = false, message = "Appointment is already cancelled" });

            // Check 24-hour rule
            if (appointment.AppointmentDateTime.HasValue)
            {
                var timeUntilAppointment = appointment.AppointmentDateTime.Value - DateTime.Now;
                if (timeUntilAppointment.TotalHours <24)
                {
                    return BadRequest(new { success = false, message = "Cannot cancel within 24 hours of appointment" });
                }
            }

            // Update status to cancelled
            appointment.Status = "cancelled";
            _db.Appointments.Update(appointment);
            _db.SaveChanges();

            // ✅ DEDUCT LOYALTY POINTS (10 points when cancelled)
            var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
            if (customer != null)
            {
                customer.LoyaltyPoint -= 10;
                // Ensure loyalty points don't go below 0
                if (customer.LoyaltyPoint < 0)
                    customer.LoyaltyPoint = 0;
                _db.Customers.Update(customer);
                _db.SaveChanges();
            }

            return Ok(new { success = true, message = "Appointment cancelled successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // Request model for cancellation
    public class CancelRequest
    {
        public required string AppointmentId { get; set; }
    }

    // Request model for appointment booking
    public class AppointmentRequest
    {
        public string Date { get; set; }
        public string Time { get; set; }
        public string ServiceId { get; set; }
        public List<string> PetIds { get; set; }
        public string Groomer { get; set; }
        public string Notes { get; set; }
    }

    [HttpGet]
    public IActionResult DownloadReceiptTxt(string appointmentId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var appointment = _db.Appointments
                .Include(a => a.Pet)
                .Include(a => a.Staff)
                .Include(a => a.Service)
                .FirstOrDefault(a => a.AppointmentId == appointmentId && a.CustomerId == userId);

            if (appointment == null)
                return NotFound();

            var customer = _db.Users.FirstOrDefault(u => u.UserId == userId);
            if (customer == null)
                return NotFound();

            string receipt = GenerateReceiptTxt(appointment, customer);
            byte[] fileBytes = Encoding.UTF8.GetBytes(receipt);

            return File(fileBytes, "text/plain", $"Receipt_{appointmentId}.txt");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public IActionResult DownloadReceiptPdf(string appointmentId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                userId = HttpContext.Session.GetString("CustomerId");
            }

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var appointment = _db.Appointments
                .Include(a => a.Pet)
                .Include(a => a.Staff)
                .Include(a => a.Service)
                .FirstOrDefault(a => a.AppointmentId == appointmentId && a.CustomerId == userId);

            if (appointment == null)
                return NotFound();

            var customer = _db.Users.FirstOrDefault(u => u.UserId == userId);
            if (customer == null)
                return NotFound();

            byte[] pdfBytes = GenerateReceiptPdf(appointment, customer);

            return File(pdfBytes, "application/pdf", $"Receipt_{appointmentId}.pdf");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]  
    public IActionResult GetMonthAppointments(int year, int month)
    {
        try
        {
            var userId = HttpContext.Session.GetString("CustomerId");
            Console.WriteLine($"GetMonthAppointments - userId from session: {userId}");
            
            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine("GetMonthAppointments - UserId is empty");
                return Json(new { success = false, appointments = new object[] { } });
            }

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            Console.WriteLine($"GetMonthAppointments - Querying: userId={userId}, range={startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            var appointments = _db.Appointments
                .Where(a => a.CustomerId == userId &&
                            a.AppointmentDateTime.HasValue &&
                            a.AppointmentDateTime.Value.Year == year &&
                            a.AppointmentDateTime.Value.Month == month)
                .AsEnumerable()
                .Select(a => new
                {
                    date = a.AppointmentDateTime.Value.ToString("yyyy-MM-dd"),
                    status = a.Status?.ToLower() ?? "unknown"
                })
                .ToList();

            Console.WriteLine($"GetMonthAppointments - Found {appointments.Count} appointments");
            foreach (var apt in appointments)
            {
                Console.WriteLine($"  {apt.date}: {apt.status}");
            }

            return Json(new { success = true, appointments = appointments });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetMonthAppointments Exception: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            return Json(new { success = false, error = ex.Message, appointments = new object[] { } });
        }
    }

    // 添加这个新方法来获取特定日期的预约
    [HttpGet]
    public async Task<IActionResult> GetAppointmentsByDate(string date)
    {
        try
        {
            var customerId = GetCurrentCustomerId(); 
            if (string.IsNullOrEmpty(customerId))
            {
                return Json(new { success = false, message = "Please login first" });
            }

            // ✅ FIX: Parse the date parameter to DateTime for database comparison
            if (!DateTime.TryParse(date, out var parsedDate))
            {
                return Json(new { success = false, message = "Invalid date format" });
            }

            // ✅ FIX: Use DateTime comparison instead of ToString()
            var appointments = await _db.Appointments
                .Where(a => a.CustomerId == customerId && 
                            a.AppointmentDateTime.HasValue &&
                            a.AppointmentDateTime.Value.Year == parsedDate.Year &&
                            a.AppointmentDateTime.Value.Month == parsedDate.Month &&
                            a.AppointmentDateTime.Value.Day == parsedDate.Day)
                .Include(a => a.Pet)
                .Include(a => a.Service)
                .Include(a => a.Staff)
                .OrderBy(a => a.AppointmentDateTime)
                .ToListAsync();

            // ✅ FIX: Do the string formatting in memory, not in the database query
            var result = appointments.Select(a => new
            {
                id = a.AppointmentId,
                time = a.AppointmentDateTime?.ToString("HH:mm"),
                petName = a.Pet?.Name,
                serviceName = a.Service?.Name,
                groomerName = a.Staff?.Name,
                status = a.Status,
                specialRequest = a.SpecialRequest
            }).ToList();

            return Json(new { success = true, appointments = result });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAppointmentsByDate Error: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    // =================================================================

    // 👇 NEW: Helper method to get current customer ID from both Claims and Session
    private string GetCurrentCustomerId()
    {
        // First, try to get from Claims (for token-based auth)
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // If not found in claims, try Session
        if (string.IsNullOrEmpty(userId))
        {
            userId = HttpContext.Session.GetString("CustomerId");
        }
        
        return userId;
    }

    // =================================================================
}