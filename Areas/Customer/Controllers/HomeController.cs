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
        return Json(new
        {
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
                        for (int i = 0; i < photoDataArray.Count; i++)
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

            var appointmentIds = new List<string>();
            var errors = new List<string>();
            int totalPointsEarned = 0;

            // ✅ Track groomer assignments for this booking to prevent duplicates within the same time slot
            var groomerAssignmentsForThisPeriod = new Dictionary<string, Models.Staff>();

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

                    // ✅ NEW: Check if this pet already has an appointment at this time with any service
                    if (DoesPetHaveConflictingAppointment(petId, appointmentDateTime, service.DurationTime ?? 0))
                    {
                        errors.Add($"Pet '{pet.Name}' already has an appointment at {appointmentDateTime:MMM dd, HH:mm}. A pet cannot have multiple services at the same time.");
                        continue;
                    }

                    // ✅ Get groomer for this specific pet from petGroomerMappings
                    Models.Staff assignedStaff = null;
                    
                    if (request.PetGroomerMappings != null && request.PetGroomerMappings.ContainsKey(petId))
                    {
                        var groomerId = request.PetGroomerMappings[petId];
                        if (!string.IsNullOrEmpty(groomerId) && groomerId != "any")
                        {
                            // ✅ Use specific groomer selected by customer
                            assignedStaff = _db.Staffs.FirstOrDefault(s => s.UserId == groomerId);

                            // ✅ Check for time conflict with this specific groomer
                            if (assignedStaff != null)
                            {
                                if (!IsGroomerAvailable(assignedStaff.UserId, appointmentDateTime, service.DurationTime ?? 0))
                                {
                                    errors.Add($"Groomer '{assignedStaff.Name}' is not available at {appointmentDateTime:MMM dd, HH:mm}. They already have an appointment during this time.");
                                    continue;
                                }

                                // ✅ Check if this groomer was already assigned in this batch
                                if (groomerAssignmentsForThisPeriod.ContainsKey(assignedStaff.UserId))
                                {
                                    errors.Add($"Groomer '{assignedStaff.Name}' cannot be assigned to multiple pets in the same time slot.");
                                    continue;
                                }
                            }
                        }
                    }

                    // If no pet-specific groomer assigned, randomly select from available staff
                    if (assignedStaff == null)
                    {
                        var availableStaff = _db.Staffs
                            .Where(s => s.Role == "staff")
                            .ToList();

                        if (availableStaff.Count > 0)
                        {
                            // ✅ Find available groomers (not already booked at this time + not already assigned in this batch)
                            var validGroomers = availableStaff
                                .Where(s => 
                                    IsGroomerAvailable(s.UserId, appointmentDateTime, service.DurationTime ?? 0) &&
                                    !groomerAssignmentsForThisPeriod.ContainsKey(s.UserId)
                                )
                                .ToList();

                            if (validGroomers.Count > 0)
                            {
                                // ✅ Randomly select one groomer
                                var random = new Random();
                                assignedStaff = validGroomers[random.Next(validGroomers.Count)];
                            }
                            else
                            {
                                errors.Add($"No available groomers for {appointmentDateTime:MMM dd, HH:mm}. All groomers are busy.");
                                continue;
                            }
                        }
                        else
                        {
                            errors.Add("No groomers available in the system");
                            continue;
                        }
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
                        StaffId = assignedStaff?.UserId,  // ✅ Use assigned groomer
                        CreatedAt = DateTime.Now
                    };

                    // Add to database
                    _db.Appointments.Add(appointment);
                    _db.SaveChanges();

                    // ✅ Track this groomer for the current batch
                    if (!groomerAssignmentsForThisPeriod.ContainsKey(assignedStaff.UserId))
                    {
                        groomerAssignmentsForThisPeriod[assignedStaff.UserId] = assignedStaff;
                    }

                    // ✅ ADD LOYALTY POINTS (10 points per pet booked)
                    customer.LoyaltyPoint += 10;
                    totalPointsEarned += 10;
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
                    message = "Failed to create any appointments: " + string.Join("; ", errors),
                    errors = errors
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
                loyaltyPointsEarned = totalPointsEarned,
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

    // ✅ NEW HELPER: Check if a pet already has a conflicting appointment at the same time
    private bool DoesPetHaveConflictingAppointment(string petId, DateTime appointmentDateTime, int durationMinutes)
    {
        var appointmentEndTime = appointmentDateTime.AddMinutes(durationMinutes);

        // Check if pet has any appointments that overlap with this time slot
        var hasConflict = _db.Appointments.Any(a =>
            a.PetId == petId &&
            a.Status != "Cancelled" &&  // Ignore cancelled appointments
            a.AppointmentDateTime.HasValue &&
            a.AppointmentDateTime < appointmentEndTime &&  // Appointment starts before this one ends
            a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) > appointmentDateTime  // Appointment ends after this one starts
        );

        return hasConflict;  // Return true if conflict found
    }

    // ✅ EXISTING HELPER: Check if groomer is available at the given time
    private bool IsGroomerAvailable(string groomerId, DateTime appointmentDateTime, int durationMinutes)
    {
        var appointmentEndTime = appointmentDateTime.AddMinutes(durationMinutes);

        // Check if groomer has any appointments that overlap with this time slot
        var hasConflict = _db.Appointments.Any(a =>
            a.StaffId == groomerId &&
            a.Status != "Cancelled" &&  // Ignore cancelled appointments
            a.AppointmentDateTime.HasValue &&
            a.AppointmentDateTime < appointmentEndTime &&  // Appointment starts before this one ends
            a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) > appointmentDateTime  // Appointment ends after this one starts
        );

        return !hasConflict;  // Return true if NO conflict found
    }

    [HttpGet]
    public IActionResult GetAppointmentSchedule()
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

            // Get the current month and year
            var now = DateTime.Now;
            var currentMonth = now.Month;
            var currentYear = now.Year;

            // Get the first day of the current month
            var startDate = new DateTime(currentYear, currentMonth, 1);

            // Get the first day of the next month
            var endDate = startDate.AddMonths(1);

            // Get appointments for the current month
            var appointments = _db.Appointments
                .Include(a => a.Pet)
                .Include(a => a.Service)
                .Where(a => a.CustomerId == userId &&
                            a.AppointmentDateTime >= startDate &&
                            a.AppointmentDateTime < endDate)
                .OrderBy(a => a.AppointmentDateTime)
                .ToList();

            // Group appointments by date
            var groupedAppointments = appointments
                .GroupBy(a => a.AppointmentDateTime.Value.Date)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    appointments = g.Select(a => new
                    {
                        a.AppointmentId,
                        a.AppointmentDateTime,
                        a.Status,
                        petName = a.Pet.Name,
                        serviceName = a.Service.Name,
                        groomerName = a.Staff != null ? a.Staff.Name : "Not assigned"
                    }).ToList()
                })
                .ToList();

            return Json(new { success = true, appointments = groupedAppointments });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetAppointmentSchedule Error: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
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
            var document = new Document(PageSize.A4, 25, 25, 25, 25);
            var writer = PdfWriter.GetInstance(document, memoryStream);
            document.Open();

            // ✅ BRAND COLORS (Darker Orange & Brown Theme)
            var brandOrange = new BaseColor(225, 123, 6);        // #E17B06 - Darker Orange (Amber-600)
            var brandOrangeDark = new BaseColor(180, 83, 9);     // #b45309 - Even Darker Orange (Amber-800)
            var brandOrangeLight = new BaseColor(245, 158, 11);  // #f59e0b - Amber-400 (lighter accent)
            var brandBeige = new BaseColor(250, 247, 242);       // #faf7f2 - Beige Background
            var darkText = new BaseColor(26, 32, 44);            // #1a202c - Dark Text
            var accentBrown = new BaseColor(120, 53, 15);        // #783509 - Darker Brown

            // Set fonts with brand colors
            var titleFont = new Font(Font.FontFamily.HELVETICA, 20, Font.BOLD, brandOrange);
            var headerFont = new Font(Font.FontFamily.HELVETICA, 12, Font.BOLD, brandOrangeDark);
            var labelFont = new Font(Font.FontFamily.HELVETICA, 10, Font.BOLD, accentBrown);
            var normalFont = new Font(Font.FontFamily.HELVETICA, 10, Font.NORMAL);
            var smallFont = new Font(Font.FontFamily.HELVETICA, 8, Font.NORMAL, new BaseColor(107, 114, 128));

            // ✅ Add colored top bar with logo
            var topTable = new PdfPTable(2);
            topTable.SetWidths(new float[] { 15, 85 });
            topTable.WidthPercentage = 100;
            topTable.DefaultCell.Border = 0;
            topTable.DefaultCell.Padding = 8;
            topTable.SpacingAfter = 15;

            try
            {
                var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "Customer", "img", "logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    var logoImage = Image.GetInstance(logoPath);
                    logoImage.ScaleToFit(120, 120);
                    topTable.AddCell(new PdfPCell(logoImage) { Border = 0, VerticalAlignment = Element.ALIGN_MIDDLE });
                }
            }
            catch { }

            var titleCell = new PdfPCell(new Phrase("PET GROOMING\nAPPOINTMENT RECEIPT", titleFont));
            titleCell.Border = 0;
            titleCell.HorizontalAlignment = Element.ALIGN_CENTER;
            titleCell.VerticalAlignment = Element.ALIGN_MIDDLE;
            titleCell.PaddingTop = 10;
            topTable.AddCell(titleCell);

            var topBgCell = topTable.DefaultCell;
            topBgCell.BackgroundColor = brandBeige;
            document.Add(topTable);

            document.Add(new Paragraph(" "));

            // ✅ Receipt ID and Date in single line with better spacing
            var infoTable = new PdfPTable(2);
            infoTable.SetWidths(new float[] { 50, 50 });
            infoTable.WidthPercentage = 100;
            infoTable.DefaultCell.Border = 0;
            infoTable.DefaultCell.Padding = 8;
            infoTable.SpacingAfter = 15;

            infoTable.AddCell(new PdfPCell(new Phrase($"Receipt #: {appointment.AppointmentId}", normalFont)) { Border = 0 });
            infoTable.AddCell(new PdfPCell(new Phrase($"Date: {DateTime.Now:MMM dd, yyyy | hh:mm tt}", normalFont)) 
            { Border = 0, HorizontalAlignment = Element.ALIGN_RIGHT });

            document.Add(infoTable);

            // ✅ Customer Section with brand orange borders
            var customerSection = new PdfPTable(1);
            customerSection.WidthPercentage = 100;
            customerSection.SpacingAfter = 15;

            var customerHeaderCell = new PdfPCell(new Phrase("👤 CUSTOMER INFORMATION", headerFont));
            customerHeaderCell.BackgroundColor = brandBeige;
            customerHeaderCell.Padding = 12;
            customerHeaderCell.BorderColor = brandOrange;
            customerHeaderCell.BorderWidth = 0;
            customerHeaderCell.BorderWidthBottom = 3;
            customerHeaderCell.PaddingLeft = 15;
            customerSection.AddCell(customerHeaderCell);

            var customerDetailsTable = new PdfPTable(2);
            customerDetailsTable.SetWidths(new float[] { 25, 75 });
            customerDetailsTable.WidthPercentage = 100;
            customerDetailsTable.DefaultCell.Border = 0;
            customerDetailsTable.DefaultCell.Padding = 10;
            customerDetailsTable.DefaultCell.BorderWidthBottom = 1f;
            customerDetailsTable.DefaultCell.BorderColorBottom = brandOrangeLight;

            // Add customer details with brand colors
            var nameLabel = new PdfPCell(new Phrase("Name", labelFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, BackgroundColor = new BaseColor(255, 250, 245), Padding = 10 };
            customerDetailsTable.AddCell(nameLabel);
            customerDetailsTable.AddCell(new PdfPCell(new Phrase(customer.Name ?? "N/A", normalFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, Padding = 10 });

            var emailLabel = new PdfPCell(new Phrase("Email", labelFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, Padding = 10 };
            customerDetailsTable.AddCell(emailLabel);
            customerDetailsTable.AddCell(new PdfPCell(new Phrase(customer.Email ?? "N/A", normalFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, BackgroundColor = new BaseColor(255, 250, 245), Padding = 10 });

            var phoneLabel = new PdfPCell(new Phrase("Phone", labelFont)) { Border = 0, BackgroundColor = new BaseColor(255, 250, 245), Padding = 10 };
            customerDetailsTable.AddCell(phoneLabel);
            customerDetailsTable.AddCell(new PdfPCell(new Phrase(customer.Phone ?? "N/A", normalFont)) { Border = 0, Padding = 10 });

            var customerDetailsCell = new PdfPCell(customerDetailsTable);
            customerDetailsCell.Border = 0;
            customerDetailsCell.Padding = 0;
            customerDetailsCell.BorderColor = brandOrange;
            customerDetailsCell.BorderWidth = 0;
            customerDetailsCell.BorderWidthLeft = 3;
            customerDetailsCell.BorderWidthRight = 3;
            customerDetailsCell.BorderWidthBottom = 3;
            customerSection.AddCell(customerDetailsCell);

            document.Add(customerSection);
            document.Add(new Paragraph(" "));

            // ✅ Appointment Section with brand orange borders
            var appointmentSection = new PdfPTable(1);
            appointmentSection.WidthPercentage = 100;
            appointmentSection.SpacingAfter = 15;

            var apptHeaderCell = new PdfPCell(new Phrase("📅 APPOINTMENT DETAILS", headerFont));
            apptHeaderCell.BackgroundColor = brandBeige;
            apptHeaderCell.Padding = 12;
            apptHeaderCell.BorderColor = brandOrange;
            apptHeaderCell.BorderWidth = 0;
            apptHeaderCell.BorderWidthBottom = 3;
            apptHeaderCell.PaddingLeft = 15;
            appointmentSection.AddCell(apptHeaderCell);

            var appointmentDetailsTable = new PdfPTable(2);
            appointmentDetailsTable.SetWidths(new float[] { 25, 75 });
            appointmentDetailsTable.WidthPercentage = 100;
            appointmentDetailsTable.DefaultCell.Border = 0;
            appointmentDetailsTable.DefaultCell.Padding = 10;
            appointmentDetailsTable.DefaultCell.BorderWidthBottom = 1f;
            appointmentDetailsTable.DefaultCell.BorderColorBottom = brandOrangeLight;

            // Add appointment details with brand colors
            var dateLabel = new PdfPCell(new Phrase("Date", labelFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, BackgroundColor = new BaseColor(255, 250, 245), Padding = 10 };
            appointmentDetailsTable.AddCell(dateLabel);
            appointmentDetailsTable.AddCell(new PdfPCell(new Phrase(appointment.AppointmentDateTime?.ToString("dddd, MMMM dd, yyyy") ?? "N/A", normalFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, Padding = 10 });

            var timeLabel = new PdfPCell(new Phrase("Time", labelFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, Padding = 10 };
            appointmentDetailsTable.AddCell(timeLabel);
            appointmentDetailsTable.AddCell(new PdfPCell(new Phrase(appointment.AppointmentDateTime?.ToString("hh:mm tt") ?? "N/A", normalFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, BackgroundColor = new BaseColor(255, 250, 245), Padding = 10 });

            var petLabel = new PdfPCell(new Phrase("Pet", labelFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, BackgroundColor = new BaseColor(255, 250, 245), Padding = 10 };
            appointmentDetailsTable.AddCell(petLabel);
            appointmentDetailsTable.AddCell(new PdfPCell(new Phrase(appointment.Pet?.Name ?? "N/A", normalFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, Padding = 10 });

            var serviceLabel = new PdfPCell(new Phrase("Service", labelFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, Padding = 10 };
            appointmentDetailsTable.AddCell(serviceLabel);
            appointmentDetailsTable.AddCell(new PdfPCell(new Phrase(appointment.Service?.Name ?? "N/A", normalFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, BackgroundColor = new BaseColor(255, 250, 245), Padding = 10 });

            var groomerLabel = new PdfPCell(new Phrase("Groomer", labelFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, BackgroundColor = new BaseColor(255, 250, 245), Padding = 10 };
            appointmentDetailsTable.AddCell(groomerLabel);
            appointmentDetailsTable.AddCell(new PdfPCell(new Phrase(appointment.Staff?.Name ?? "Not assigned", normalFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, Padding = 10 });

            var durationLabel = new PdfPCell(new Phrase("Duration", labelFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, Padding = 10 };
            appointmentDetailsTable.AddCell(durationLabel);
            appointmentDetailsTable.AddCell(new PdfPCell(new Phrase($"{appointment.DurationTime} minutes", normalFont)) { Border = 0, BorderWidthBottom = 1f, BorderColorBottom = brandOrangeLight, BackgroundColor = new BaseColor(255, 250, 245), Padding = 10 });

            var statusLabel = new PdfPCell(new Phrase("Status", labelFont)) { Border = 0, BackgroundColor = new BaseColor(255, 250, 245), Padding = 10 };
            appointmentDetailsTable.AddCell(statusLabel);
            var statusColor = appointment.Status?.ToLower() == "confirmed" ? brandOrange : new BaseColor(239, 68, 68);
            var statusCell = new PdfPCell(new Phrase(appointment.Status ?? "N/A", new Font(Font.FontFamily.HELVETICA, 10, Font.BOLD, statusColor))) { Border = 0, HorizontalAlignment = Element.ALIGN_CENTER, Padding = 10 };
            appointmentDetailsTable.AddCell(statusCell);

            var appointmentDetailsCell = new PdfPCell(appointmentDetailsTable);
            appointmentDetailsCell.Border = 0;
            appointmentDetailsCell.Padding = 0;
            appointmentDetailsCell.BorderColor = brandOrange;
            appointmentDetailsCell.BorderWidth = 0;
            appointmentDetailsCell.BorderWidthLeft = 3;
            appointmentDetailsCell.BorderWidthRight = 3;
            appointmentDetailsCell.BorderWidthBottom = 3;
            appointmentSection.AddCell(appointmentDetailsCell);

            document.Add(appointmentSection);

            // ✅ Special Requests Section (if exists)
            if (!string.IsNullOrEmpty(appointment.SpecialRequest))
            {
                var notesSection = new PdfPTable(1);
                notesSection.WidthPercentage = 100;
                notesSection.SpacingAfter = 15;

                var notesHeaderCell = new PdfPCell(new Phrase("📝 SPECIAL REQUESTS", headerFont));
                notesHeaderCell.BackgroundColor = brandBeige;
                notesHeaderCell.Padding = 12;
                notesHeaderCell.BorderColor = accentBrown;
                notesHeaderCell.BorderWidth = 0;
                notesHeaderCell.BorderWidthBottom = 3;
                notesHeaderCell.PaddingLeft = 15;
                notesSection.AddCell(notesHeaderCell);

                var notesCell = new PdfPCell(new Phrase(appointment.SpecialRequest, normalFont));
                notesCell.Padding = 15;
                notesCell.Border = 0;
                notesCell.BorderColor = accentBrown;
                notesCell.BorderWidth = 0;
                notesCell.BorderWidthLeft = 3;
                notesCell.BorderWidthRight = 3;
                notesCell.BorderWidthBottom = 3;
                notesCell.HorizontalAlignment = Element.ALIGN_JUSTIFIED;
                notesSection.AddCell(notesCell);

                document.Add(notesSection);
                document.Add(new Paragraph(" "));
            }

            document.Add(new Paragraph(" "));

            // ✅ Footer with brand styling
            var footerTable = new PdfPTable(1);
            footerTable.WidthPercentage = 100;
            footerTable.DefaultCell.Border = 0;
            footerTable.DefaultCell.Padding = 0;
            footerTable.DefaultCell.BackgroundColor = brandBeige;

            var footer1 = new PdfPCell(new Phrase("Thank you for choosing our service!", 
                new Font(Font.FontFamily.HELVETICA, 11, Font.BOLD, brandOrange)));
            footer1.Border = 0;
            footer1.Padding = 12;
            footer1.HorizontalAlignment = Element.ALIGN_CENTER;
            footerTable.AddCell(footer1);

            var footer2 = new PdfPCell(new Phrase("We look forward to seeing you and your furry friend again! 🐾", smallFont));
            footer2.Border = 0;
            footer2.Padding = 8;
            footer2.HorizontalAlignment = Element.ALIGN_CENTER;
            footerTable.AddCell(footer2);

            document.Add(footerTable);

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

        int nextNumber = 1;

        if (lastAppointment != null && lastAppointment.AppointmentId.StartsWith("AP"))
        {
            // Extract the number from the last ID (e.g., "AP001" -> 1)
            if (int.TryParse(lastAppointment.AppointmentId.Substring(2), out int lastNumber))
            {
                nextNumber = lastNumber + 1;
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

                if (timeUntilAppointment.TotalHours < 24)
                {
                    var hoursRemaining = Math.Round(timeUntilAppointment.TotalHours, 1);
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
                if (timeUntilAppointment.TotalHours < 24)
                {
                    return BadRequest(new { success = false, message = "Cannot cancel within 24 hours of appointment" });
                }
            }

            // Update status to cancelled
            appointment.Status = "Cancelled";
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
        public Dictionary<string, string> PetGroomerMappings { get; set; }  // ✅ Add this
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
            // ✅ UPDATED: Add petType to the response
            var result = appointments.Select(a => new
            {
                id = a.AppointmentId,
                time = a.AppointmentDateTime?.ToString("HH:mm"),
                petName = a.Pet?.Name,
                petType = a.Pet?.Type?.ToLower(), // ✅ NEW: Include pet type (dog/cat)
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

    // ✅ NEW: Check groomer availability for specific date and time
    [HttpGet]
    public IActionResult CheckGroomerAvailability(string date, string time, int durationMinutes)
    {
        try
        {
            Console.WriteLine($"🔍 CheckGroomerAvailability called: date='{date}', time='{time}', duration={durationMinutes}");

            // ✅ FIXED: Handle multiple date formats
            DateTime appointmentDateTime;
            
            // Try parsing as ISO format first (yyyy-MM-dd)
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                // Fallback to general parse
                if (!DateTime.TryParse(date, out parsedDate))
                {
                    Console.WriteLine($"❌ Failed to parse date: {date}");
                    return BadRequest(new { success = false, message = "Invalid date format" });
                }
            }

            // Parse time
            if (!DateTime.TryParseExact(time, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedTime))
            {
                // Fallback to general parse
                if (!DateTime.TryParse(time, out parsedTime))
                {
                    Console.WriteLine($"❌ Failed to parse time: {time}");
                    return BadRequest(new { success = false, message = "Invalid time format" });
                }
            }

            // Combine date and time
            appointmentDateTime = parsedDate.Date.Add(parsedTime.TimeOfDay);
            
            Console.WriteLine($"✅ Parsed appointmentDateTime: {appointmentDateTime:yyyy-MM-dd HH:mm}");

            var appointmentEndTime = appointmentDateTime.AddMinutes(durationMinutes);
            Console.WriteLine($"⏱️ Time range: {appointmentDateTime:HH:mm} - {appointmentEndTime:HH:mm}");

            // Get all groomer IDs
            var allGroomerIds = _db.Staffs
                .Where(s => s.Role == "staff")
                .Select(s => s.UserId)
                .ToList();

            Console.WriteLine($"Total groomers: {allGroomerIds.Count}");

            // Filter out groomers with conflicts
            var availableGroomerIds = new List<string>();

            foreach (var groomerId in allGroomerIds)
            {
                // Check for conflicts on the SAME DATE
                var hasConflict = _db.Appointments.Any(a =>
                    a.StaffId == groomerId &&
                    a.Status != "Cancelled" &&
                    a.AppointmentDateTime.HasValue &&
                    // ✅ CRITICAL: Only check SAME DATE
                    a.AppointmentDateTime.Value.Date == appointmentDateTime.Date &&
                    // ✅ Check time overlap
                    a.AppointmentDateTime < appointmentEndTime &&
                    a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) > appointmentDateTime
                );

                if (!hasConflict)
                {
                    availableGroomerIds.Add(groomerId);
                    Console.WriteLine($"✅ Groomer {groomerId} is AVAILABLE on {appointmentDateTime:yyyy-MM-dd}");
                }
                else
                {
                    Console.WriteLine($"❌ Groomer {groomerId} has conflict on {appointmentDateTime:yyyy-MM-dd}");
                }
            }

            Console.WriteLine($"📊 Available groomers: {availableGroomerIds.Count} out of {allGroomerIds.Count}");

            return Json(new { success = true, availableGroomerIds = availableGroomerIds });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ CheckGroomerAvailability Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ✅ NEW: Validate reschedule time is within 9 AM - 4:30 PM
    private bool IsValidRescheduleTime(DateTime newDateTime)
    {
        var timeOfDay = newDateTime.TimeOfDay;
        var minTime = new TimeSpan(9, 0, 0);      // 9:00 AM
        var maxTime = new TimeSpan(16, 30, 0);    // 4:30 PM

        return timeOfDay >= minTime && timeOfDay <= maxTime;
    }

    // ✅ NEW: Reschedule Appointment endpoint
    [HttpPost]
    public IActionResult RescheduleAppointment([FromBody] RescheduleRequest request)
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

            // Check if already cancelled or completed
            if (appointment.Status?.ToLower() == "cancelled" || appointment.Status?.ToLower() == "completed")
                return BadRequest(new { success = false, message = $"Cannot reschedule a {appointment.Status} appointment" });

            // Parse new date and time
            if (!DateTime.TryParse($"{request.NewDate} {request.NewTime}", out var newDateTime))
                return BadRequest(new { success = false, message = "Invalid date or time format" });

            // ✅ Validate time is within 9 AM - 4:30 PM
            if (!IsValidRescheduleTime(newDateTime))
                return BadRequest(new { success = false, message = "Appointment time must be between 9:00 AM and 4:30 PM" });

            // Verify new appointment is in the future
            if (newDateTime <= DateTime.Now)
                return BadRequest(new { success = false, message = "Appointment date must be in the future" });

            // ✅ Check if pet has conflicting appointment at new time
            if (DoesPetHaveConflictingAppointment(appointment.PetId, newDateTime, appointment.DurationTime ?? 0))
                return BadRequest(new { success = false, message = $"Pet already has an appointment at the new time. Please select a different time." });

            // ✅ Check if assigned groomer is available at new time
            if (!string.IsNullOrEmpty(appointment.StaffId) && 
                !IsGroomerAvailable(appointment.StaffId, newDateTime, appointment.DurationTime ?? 0))
            {
                return BadRequest(new { success = false, message = "Assigned groomer is not available at the new time. Please select a different time." });
            }

            // Update appointment
            appointment.AppointmentDateTime = newDateTime;
            _db.Appointments.Update(appointment);
            _db.SaveChanges();

            return Ok(new { success = true, message = "Appointment rescheduled successfully!" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RescheduleAppointment Error: {ex.Message}");
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // Request model for rescheduling
    public class RescheduleRequest
    {
        public string AppointmentId { get; set; }
        public string NewDate { get; set; }
        public string NewTime { get; set; }
    }

    // ✅ NEW: Validate appointment is within business hours (9 AM - 4:30 PM)
    private bool IsWithinBusinessHours(DateTime appointmentDateTime, int durationMinutes)
    {
        var timeOfDay = appointmentDateTime.TimeOfDay;
        var endTimeOfDay = appointmentDateTime.AddMinutes(durationMinutes).TimeOfDay;
        
        var businessStart = new TimeSpan(9, 0, 0);      // 9:00 AM
        var businessEnd = new TimeSpan(16, 30, 0);      // 4:30 PM

        // 检查预约开始时间在营业时间内
        if (timeOfDay < businessStart)
            return false;
        
        // 检查预约结束时间不超过营业时间
        if (endTimeOfDay > businessEnd)
            return false;

        return true;
    }
}