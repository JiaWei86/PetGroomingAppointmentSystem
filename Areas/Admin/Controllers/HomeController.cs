using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Areas.Admin.Controllers;
using PetGroomingAppointmentSystem.Areas.Admin.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace PetGroomingAppointmentSystem.Areas.Admin.Controllers;

[Area("Admin")]
[AdminOnly]
public class HomeController : Controller
{
    private readonly DB _db;
    private readonly IEmailService _emailService;
    private readonly IPasswordService _passwordService;
    private readonly IPhoneService _phoneService;
    private readonly IValidationService _validationService;

    public HomeController(
    DB db,
    IEmailService emailService,
    IPasswordService passwordService,
    IPhoneService phoneService,
    IValidationService validationService)
    {
        _db = db;
        _emailService = emailService;
        _passwordService = passwordService;
        _phoneService = phoneService;
        _validationService = validationService;

    }
    public class FieldValidationRequest
    {
        public string StaffId { get; set; }
        public string FieldName { get; set; }
        public string FieldValue { get; set; }
    }


    // ========== DASHBOARD
    public IActionResult Index()
    {
        ViewData["ActivePage"] = "Dashboard";
        return View();
    }

    /// <summary>
    /// AJAX endpoint for real-time field validation for the Groomer edit form.
    /// </summary>
    [HttpPost]
    // [ValidateAntiForgeryToken] // Removed for this specific AJAX call to simplify client-side logic.
    public async Task<IActionResult> ValidateGroomerField([FromBody] FieldValidationRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FieldName) || string.IsNullOrWhiteSpace(request.FieldValue))
            {
                return Json(new { isValid = false, errorMessage = "Invalid validation request." });
            }

            bool isDuplicate = false;
            string errorMessage = "";
            var valueToCheck = request.FieldValue.Trim();

            switch (request.FieldName.ToLower())
            {
                case "name":
                    if (!_validationService.ValidateName(valueToCheck))
                    {
                        return Json(new { isValid = false, errorMessage = "Name must be 3-200 characters and contain only letters/spaces." });
                    }
                    break;

                case "ic":
                    if (!_validationService.ValidateICFormat(valueToCheck))
                    {
                        return Json(new { isValid = false, errorMessage = "IC format must be xxxxxx-xx-xxxx and age 18-60." });
                    }
                    // This validation is for EDIT mode only, so StaffId is required.
                    isDuplicate = await _db.Staffs.AnyAsync(s => s.IC == valueToCheck && s.UserId != request.StaffId);

                    if (isDuplicate)
                    {
                        errorMessage = "This IC number is already registered.";
                    }
                    break;

                case "email":
                    if (!_validationService.ValidateEmail(valueToCheck))
                    {
                        return Json(new { isValid = false, errorMessage = "Please enter a valid email address." });
                    }
                    // This validation is for EDIT mode only, so StaffId is required.
                    isDuplicate = await _db.Staffs.AnyAsync(s => s.Email.ToLower() == valueToCheck.ToLower() && s.UserId != request.StaffId);

                    if (isDuplicate)
                    {
                        errorMessage = "This email address is already in use.";
                    }
                    break;

                case "phone":
                    string formattedPhone = _phoneService.FormatPhoneNumber(valueToCheck);
                    if (!_phoneService.ValidatePhoneFormat(formattedPhone))
                    {
                        return Json(new { isValid = false, errorMessage = "Phone format must be 01X-XXXXXXX or 01X-XXXXXXXX." });
                    }
                    // This validation is for EDIT mode only, so StaffId is required.
                    isDuplicate = await _db.Staffs.AnyAsync(s => s.Phone == formattedPhone && s.UserId != request.StaffId);

                    if (isDuplicate)
                    {
                        errorMessage = "This phone number is already registered.";
                    }
                    break;

                default:
                    return Json(new { isValid = false, errorMessage = "Invalid field for validation." });
            }

            if (isDuplicate)
            {
                return Json(new { isValid = false, errorMessage = errorMessage });
            }

            return Json(new { isValid = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Validation error: {ex.Message}");
            // Return a generic error to the client
            return Json(new { isValid = false, errorMessage = "An unexpected error occurred during validation." });
        }
    }


    // ========== GROOMER
    // GET: List all groomers
    public async Task<IActionResult> Groomer()
    {
        ViewData["ActivePage"] = "Groomer";
        var groomers = await _db.Staffs
            .OrderByDescending(s => s.UserId)
    .ToListAsync();
        return View(groomers);
    }

    // POST: Create/Edit/Delete Groomer
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Groomer(
          Models.Staff staff,
        string actionType,
          string editStaffId,
          string deleteStaffId,
          IFormFile PhotoUpload)
    {
        // --- CREATE ---
        if (actionType == "add")
        {
            // ========== VALIDATE INPUTS USING VALIDATION SERVICE =========

            // Validate Name
            if (!_validationService.ValidateName(staff.Name))
            {
                ViewData["Error"] = "Name must be between 3-200 characters and contain only letters and spaces.";
                var groomers = await _db.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
                return View(groomers);
            }

            // Validate IC
            if (!_validationService.ValidateICFormat(staff.IC))
            {
                ViewData["Error"] = "IC number must be in format xxxxxx-xx-xxxx (e.g., 123456-78-9012).";
                var groomers = await _db.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
                return View(groomers);
            }

            // Validate Email
            if (!_validationService.ValidateEmail(staff.Email))
            {
                ViewData["Error"] = "Please enter a valid email address.";
                var groomers = await _db.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
                return View(groomers);
            }

            // Format and validate Phone using Phone Service
            string formattedPhoneNumber = _phoneService.FormatPhoneNumber(staff.Phone);

            if (!_phoneService.ValidatePhoneFormat(formattedPhoneNumber))
            {
                ViewData["Error"] = "Phone number must be in format 01X-XXXXXXX or 01X-XXXXXXXX (e.g., 012-1234567).";
                var groomers = await _db.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
                return View(groomers);
            }

            // Check for duplicates
            if (await _db.Staffs.AnyAsync(s => s.Phone == formattedPhoneNumber))
            {
                ViewData["Error"] = "This phone number is already registered.";
                var groomers = await _db.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
                return View(groomers);
            }

            if (await _db.Staffs.AnyAsync(s => s.Email == staff.Email))
            {
                ViewData["Error"] = "This email address is already registered.";
                var groomers = await _db.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
                return View(groomers);
            }

            if (await _db.Staffs.AnyAsync(s => s.IC == staff.IC))
            {
                ViewData["Error"] = "This IC number is already registered.";
                var groomers = await _db.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
                return View(groomers);
            }

            // Validate Position using Validation Service
            if (!_validationService.ValidatePosition(staff.Position)
)
            {
                ViewData["Error"] = "Please select a valid position.";
                var groomers = await _db.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
                return View(groomers);
            }

            // Update phone number to formatted version
            staff.Phone = formattedPhoneNumber;

            // ========== SMART ID GENERATION ==========
            var allStaffIds = await _db.Staffs
  .Select(s => s.UserId)
     .OrderBy(id => id)
          .ToListAsync();

            string newStaffId;

            if (!allStaffIds.Any())
            {
                newStaffId = "S001";
            }
            else
            {
                var usedNumbers = allStaffIds
                   .Select(id => int.Parse(id.Substring(1)))
                   .OrderBy(n => n)
           .ToList();

                int nextNumber = 1;
                bool foundGap = false;

                foreach (var num in usedNumbers)
                {
                    if (num != nextNumber)
                    {
                        foundGap = true;
                        break;
                    }
                    nextNumber++;
                }

                if (!foundGap)
                {
                    nextNumber = usedNumbers.Max() + 1;
                }

                newStaffId = "S" + nextNumber.ToString("D3");
            }

            // Get Admin ID from session
            string currentAdminId = HttpContext.Session.GetString("AdminId");

            if (string.IsNullOrEmpty(currentAdminId))
            {
                ViewData["Error"] = "❌ Admin not logged in. Please login again.";
                var groomers = await _db.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
                return View(groomers);
            }

            // ========== GENERATE RANDOM PASSWORD USING PASSWORD SERVICE ==========
            string temporaryPassword = _passwordService.GenerateRandomPassword(12);

            // ========== HANDLE PHOTO UPLOAD ==========
            if (PhotoUpload != null && PhotoUpload.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(PhotoUpload.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PhotoUpload.CopyToAsync(stream);
                }

                staff.Photo = "/uploads/" + fileName;
            }
            else
            {
                staff.Photo = "/uploads/placeholder.png";
            }

            // Set all fields
            staff.UserId = newStaffId;
            staff.Role = "staff";
            staff.Password = temporaryPassword;
            staff.CreatedAt = DateTime.Now;
            staff.AdminUserId = currentAdminId;
            staff.Description = staff.Description ?? "";

            // Add to Staffs
            _db.Staffs.Add(staff);
            await _db.SaveChangesAsync();

            // ========== SEND EMAIL WITH CREDENTIALS USING EMAIL SERVICE ==========
            try
            {
                var loginUrl = $"{Request.Scheme}://{Request.Host}/Admin/Auth/Login";

                // Defensive check: if another staff (different id) already has this email, skip sending credentials.
                bool duplicateAfterSave = await _db.Staffs.AnyAsync(s => s.Email == staff.Email && s.UserId != newStaffId);
                if (duplicateAfterSave)
                {
                    Console.WriteLine($"[WARN] CreateGroomerAjax: skipping credentials email for {staff.Email} because duplicate exists after save.");
                    return Json(new { success = true, message = $" Staff {staff.Name} created successfully! (credentials email skipped - duplicate email)", staffId = newStaffId });
                }

                await _emailService.SendStaffCredentialsEmailAsync(
                    toEmail: staff.Email,
                    staffName: staff.Name,
                    staffId: newStaffId,
                    temporaryPassword: temporaryPassword,
                    email: staff.Email,
                    phone: staff.Phone,
                    loginUrl: loginUrl
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email send failed for {staff.Email}: {ex.Message}");
            }

            TempData["SuccessMessage"] = $" Staff {staff.Name} created successfully!";
            return RedirectToAction(nameof(Groomer));
        }

        // --- EDIT ---
        else if (actionType == "edit")
        {
            if (string.IsNullOrEmpty(editStaffId)) return NotFound();

            var dbStaff = await _db.Staffs.FindAsync(editStaffId);
            if (dbStaff == null) return NotFound();

            // ========== VALIDATE INPUTS USING VALIDATION SERVICE (与 ADD 一致) ==========

            // Validate Name
            if (!_validationService.ValidateName(staff.Name))
            {
                TempData["ErrorMessage"] = "Name must be between 3-200 characters and contain only letters and spaces.";
                return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
            }

            // Validate IC
            if (!_validationService.ValidateICFormat(staff.IC))
            {
                TempData["ErrorMessage"] = "IC number must be in format xxxxxx-xx-xxxx (e.g., 123456-78-9012).";
                return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
            }

            // Validate Email
            if (!_validationService.ValidateEmail(staff.Email))
            {
                TempData["ErrorMessage"] = "Please enter a valid email address.";
                return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
            }

            // Format and validate Phone using Phone Service
            string formattedPhoneNumber = _phoneService.FormatPhoneNumber(staff.Phone);

            if (!_phoneService.ValidatePhoneFormat(formattedPhoneNumber))
            {
                TempData["ErrorMessage"] = "Phone number must be in format 01X-XXXXXXX or 01X-XXXXXXXX (e.g., 012-1234567).";
                return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
            }

            // Check for duplicates (excluding current staff)
            if (await _db.Staffs.AnyAsync(s => s.Phone == formattedPhoneNumber && s.UserId != editStaffId))
            {
                TempData["ErrorMessage"] = "This phone number is already registered.";
                return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
            }

            if (await _db.Staffs.AnyAsync(s => s.Email == staff.Email && s.UserId != editStaffId))
            {
                TempData["ErrorMessage"] = "This email address is already registered.";
                return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
            }

            if (await _db.Staffs.AnyAsync(s => s.IC == staff.IC && s.UserId != editStaffId))
            {
                TempData["ErrorMessage"] = "This IC number is already registered.";
                return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
            }

            // Validate Position using Validation Service
            if (!_validationService.ValidatePosition(staff.Position))
            {
                TempData["ErrorMessage"] = "Please select a valid position.";
                return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
            }

            // Validate Experience Year (optional field)
            if (staff.ExperienceYear.HasValue && !_validationService.ValidateExperienceYear(staff.ExperienceYear))
            {
                TempData["ErrorMessage"] = "Experience year must be between 0 and 50.";
                return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
            }

            // Update phone number to formatted version
            staff.Phone = formattedPhoneNumber;

            // Update fields
            dbStaff.Name = staff.Name;
            dbStaff.Email = staff.Email;
            dbStaff.Phone = staff.Phone;
            dbStaff.IC = staff.IC;
            dbStaff.Description = staff.Description ?? "";
            dbStaff.ExperienceYear = staff.ExperienceYear;
            dbStaff.Position = staff.Position;

            // Handle photo upload in edit
            if (PhotoUpload != null && PhotoUpload.Length > 0)
            {
                // Validate file size (max 5MB)
                if (PhotoUpload.Length > 5 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "Photo file size must not exceed 5MB.";
                    return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(PhotoUpload.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData["ErrorMessage"] = "Only image files (JPG, JPEG, PNG, GIF) are allowed.";
                    return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
                }

                var fileName = Guid.NewGuid() + fileExtension;
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PhotoUpload.CopyToAsync(stream);
                }

                // Delete old photo if exists (except placeholder)
                if (!string.IsNullOrEmpty(dbStaff.Photo) && dbStaff.Photo != "/uploads/placeholder.png")
                {
                    var oldPhotoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", dbStaff.Photo.TrimStart('/'));
                    if (System.IO.File.Exists(oldPhotoPath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldPhotoPath);
                        }
                        catch (Exception ex)
                        {
                            // Log error but don't fail the operation
                            Console.WriteLine($"[WARNING] Failed to delete old photo: {ex.Message}");
                        }
                    }
                }

                dbStaff.Photo = "/uploads/" + fileName;
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $" Groomer '{staff.Name}' updated successfully!";
            return RedirectToAction(nameof(Groomer));
        }

        // --- DELETE ---
        else if (actionType == "delete")
        {
            if (string.IsNullOrEmpty(deleteStaffId)) return NotFound();

            var dbStaff = await _db.Staffs.FindAsync(deleteStaffId);
            if (dbStaff == null) return NotFound();

            _db.Staffs.Remove(dbStaff);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = " Staff deleted successfully!";

            return RedirectToAction(nameof(Groomer));
        }

        // Fallback
        var allGroomers = await _db.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
        return View(allGroomers);
    }

    // ========== CUSTOMER ==========
    public IActionResult Customer()
    {
        ViewData["ActivePage"] = "Customer";
        return View();
    }

    // ========== APPOINTMENT ==========
    public IActionResult appointment()
    {
        ViewData["ActivePage"] = "Appoinments";
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

    // ========== REDEEM GIFT ==========
    // LIST & optionally CREATE (GET)
    public async Task<IActionResult> RedeemGift()
    {
        ViewData["ActivePage"] = "RedeemGift";
        var gifts = await _db.RedeemGifts.OrderByDescending(g => g.GiftId).ToListAsync();
        return View(gifts);
    }

    // CREATE (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RedeemGift(
        RedeemGift gift,
        string actionType,
        IFormFile PhotoUpload,
        string editGiftId,
        string deleteGiftId)
    {
        // --- CREATE ---
        if (actionType == "add")
        {
            // ========== SMART ID GENERATION ==========
            var allGiftIds = await _db.RedeemGifts
                .Select(g => g.GiftId)
                .OrderBy(id => id)
                .ToListAsync();

            string newGiftId;

            if (!allGiftIds.Any())
            {
                newGiftId = "G001";
            }
            else
            {
                var usedNumbers = allGiftIds
                    .Select(id => int.Parse(id.Substring(1)))
                    .OrderBy(n => n)
                    .ToList();

                int nextNumber = 1;
                bool foundGap = false;

                foreach (var num in usedNumbers)
                {
                    if (num != nextNumber)
                    {
                        foundGap = true;
                        break;
                    }
                    nextNumber++;
                }

                if (!foundGap)
                {
                    nextNumber = usedNumbers.Max() + 1;
                }

                newGiftId = "G" + nextNumber.ToString("D3");
            }

            gift.GiftId = newGiftId;
            gift.AdminId = HttpContext.Session.GetString("AdminId");

            if (string.IsNullOrEmpty(gift.AdminId))
            {
                ViewData["Error"] = "❌ Unable to save: Admin is not logged in. Please login again.";
                var list = await _db.RedeemGifts
                    .OrderByDescending(g => g.GiftId)
                    .ToListAsync();
                return View(list);
            }

            if (PhotoUpload != null && PhotoUpload.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(PhotoUpload.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);
                var filePath = Path.Combine(uploadPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await PhotoUpload.CopyToAsync(stream);
                gift.Photo = "/uploads/" + fileName;
            }
            else
            {
                gift.Photo = "/uploads/placeholder.png";
            }

            _db.RedeemGifts.Add(gift);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $" Gift '{gift.Name}' created successfully!";
            return RedirectToAction(nameof(RedeemGift));
        }

        // --- EDIT ---
        else if (actionType == "edit")
        {
            if (editGiftId == null) return NotFound();
            var dbGift = await _db.RedeemGifts.FindAsync(editGiftId);
            if (dbGift == null) return NotFound();

            dbGift.Name = gift.Name;
            dbGift.Quantity = gift.Quantity;
            dbGift.LoyaltyPointCost = gift.LoyaltyPointCost;

            if (PhotoUpload != null && PhotoUpload.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(PhotoUpload.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);
                var filePath = Path.Combine(uploadPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PhotoUpload.CopyToAsync(stream);
                }
                dbGift.Photo = "/uploads/" + fileName;
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $" Gift '{gift.Name}' updated successfully!";
            return RedirectToAction(nameof(RedeemGift));
        }

        // --- DELETE ---
        else if (actionType == "delete")
        {
            if (deleteGiftId == null) return NotFound();
            var dbGift = await _db.RedeemGifts.FindAsync(deleteGiftId);
            if (dbGift == null) return NotFound();
            _db.RedeemGifts.Remove(dbGift);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = " Gift deleted successfully!";
            return RedirectToAction(nameof(RedeemGift));
        }

        // --- FALLBACK ---
        var gifts = await _db.RedeemGifts.OrderByDescending(g => g.GiftId).ToListAsync();
        return View(gifts);
    }

    // ========== SERVICE
    // GET: List all services
    public async Task<IActionResult> Service(string ServiceName, string Category, string Status, string editId)
    {
        ViewData["ActivePage"] = "Service";
        var query = _db.Services
            .Include(s => s.ServiceServiceCategories).ThenInclude(ssc => ssc.Category)
            .AsQueryable();

        if (!string.IsNullOrEmpty(ServiceName))
            query = query.Where(s => s.Name.Contains(ServiceName));

        if (!string.IsNullOrEmpty(Category))
            query = query.Where(s => s.ServiceServiceCategories.Any(ssc => ssc.Category.Name.Contains(Category)));

        if (!string.IsNullOrEmpty(Status))
            query = query.Where(s => s.Status == Status);

        var services = await query.OrderByDescending(s => s.ServiceId).ToListAsync();

        // Load categories for dropdowns in the view
        var categories = await _db.ServiceCategories.OrderBy(c => c.CategoryId).ToListAsync();
        ViewBag.ServiceCategories = categories;

        // Pass editId to view
        ViewBag.EditId = editId;

        return View(services);
    }

    // POST: Create/Edit/Delete Service

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Service(
        Service service,
        string actionType,
        string editServiceId,
        string deleteServiceId,
        string[] SelectedCategories)
    {
        // ================= CREATE =================
        if (actionType == "create")
        {
            string adminId = HttpContext.Session.GetString("AdminId");
            if (string.IsNullOrEmpty(adminId))
            {
                TempData["ErrorMessage"] = "Admin not logged in. Please login again.";
                return RedirectToAction(nameof(Service));
            }

            // Generate ServiceId
            var lastService = await _db.Services
                .OrderByDescending(s => s.ServiceId)
                .FirstOrDefaultAsync();

            string newServiceId = "SE001";
            if (lastService != null)
            {
                int lastNum = int.Parse(lastService.ServiceId.Substring(2));
                newServiceId = $"SE{(lastNum + 1):D3}";
            }

            service.ServiceId = newServiceId;
            service.AdminId = adminId;
            service.Status = service.Status ?? "Active";

            _db.Services.Add(service);
            await _db.SaveChangesAsync(); // 必须先存 Service

            // ===== CREATE SSC =====
            if (SelectedCategories != null && SelectedCategories.Length > 0)
            {
                // Find all existing SSC numeric parts
                var allSscIds = await _db.ServiceServiceCategories
                .Where(x => !string.IsNullOrEmpty(x.SscId))
                .Select(x => x.SscId)
                .ToListAsync();

                var usedNumbers = allSscIds
                .Select(id =>
                {
                    int n;
                    return int.TryParse(id.Substring(3), out n) ? n : 0;
                })
                .Where(n => n > 0)
                .OrderBy(n => n)
                .ToList();

                // Helper to get next available (smallest missing) number starting from1
                int GetNextAvailable(ref List<int> used, int start)
                {
                    int candidate = start;
                    var set = new HashSet<int>(used);
                    while (set.Contains(candidate)) candidate++;
                    // reserve it by adding to used (so next call won't reuse)
                    used.Add(candidate);
                    used.Sort();
                    return candidate;
                }

                // We'll start allocating from1 and fill gaps
                var usedClone = new List<int>(usedNumbers);

                foreach (var catId in SelectedCategories)
                {
                    int nextNum = GetNextAvailable(ref usedClone, 1);
                    var ssc = new ServiceServiceCategory
                    {
                        SscId = $"SSC{nextNum:D3}",
                        ServiceId = service.ServiceId,
                        CategoryId = catId
                    };

                    _db.ServiceServiceCategories.Add(ssc);
                }

                await _db.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"Service '{service.Name}' created successfully!";
            return RedirectToAction(nameof(Service));
        }

        // ================= EDIT =================
        else if (actionType == "edit")
        {
            if (string.IsNullOrEmpty(editServiceId))
                return NotFound();

            var dbService = await _db.Services
                .Include(s => s.ServiceServiceCategories)
                .FirstOrDefaultAsync(s => s.ServiceId == editServiceId);

            if (dbService == null)
                return NotFound();

            dbService.Name = service.Name;
            dbService.Price = service.Price;
            dbService.DurationTime = service.DurationTime;
            dbService.Status = service.Status;
            dbService.Description = service.Description;

            // Remove old SSC
            if (dbService.ServiceServiceCategories.Any())
            {
                _db.ServiceServiceCategories.RemoveRange(dbService.ServiceServiceCategories);
                await _db.SaveChangesAsync();
            }

            // Add new SSC
            if (SelectedCategories != null && SelectedCategories.Length > 0)
            {
                // Find all existing SSC numeric parts (after removal above)
                var allSscIds = await _db.ServiceServiceCategories
                .Where(x => !string.IsNullOrEmpty(x.SscId))
                .Select(x => x.SscId)
                .ToListAsync();

                var usedNumbers = allSscIds
                .Select(id =>
                {
                    int n;
                    return int.TryParse(id.Substring(3), out n) ? n : 0;
                })
                .Where(n => n > 0)
                .OrderBy(n => n)
                .ToList();

                int GetNextAvailable(ref List<int> used, int start)
                {
                    int candidate = start;
                    var set = new HashSet<int>(used);
                    while (set.Contains(candidate)) candidate++;
                    used.Add(candidate);
                    used.Sort();
                    return candidate;
                }

                var usedClone = new List<int>(usedNumbers);

                foreach (var catId in SelectedCategories)
                {
                    int nextNum = GetNextAvailable(ref usedClone, 1);
                    var ssc = new ServiceServiceCategory
                    {
                        SscId = $"SSC{nextNum:D3}",
                        ServiceId = dbService.ServiceId,
                        CategoryId = catId
                    };

                    _db.ServiceServiceCategories.Add(ssc);
                }

                await _db.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"Service '{service.Name}' updated successfully!";
            return RedirectToAction(nameof(Service));
        }

        // ================= DELETE =================
        else if (actionType == "delete")
        {
            if (string.IsNullOrEmpty(deleteServiceId))
                return NotFound();

            var dbService = await _db.Services.FindAsync(deleteServiceId);
            if (dbService == null)
                return NotFound();

            _db.Services.Remove(dbService);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Service deleted successfully!";
            return RedirectToAction(nameof(Service));
        }

        return RedirectToAction(nameof(Service));
    }


    public IActionResult reports()
    {
        ViewData["ActivePage"] = "reports";
        return View();
    }

    /// <summary>
    /// AJAX endpoint for creating groomer with validation
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroomerAjax(
     [FromForm] Models.Staff staff,
        [FromForm] IFormFile PhotoUpload)
    {
        try
        {
            // ========== VALIDATE INPUTS USING SERVICES =========
            var errors = new Dictionary<string, string>();

            // Validate Name using ValidationService
            if (!_validationService.ValidateName(staff.Name))
            {
                if (string.IsNullOrWhiteSpace(staff.Name))
                    errors["Name"] = "Full name cannot be empty.";
                else if (staff.Name.Length < 3 || staff.Name.Length > 200)
                    errors["Name"] = "Name must be between 3-200 characters.";
                else
                    errors["Name"] = "Name must contain only letters and spaces.";
            }

            // Validate IC using ValidationService
            if (string.IsNullOrWhiteSpace(staff.IC))
                errors["IC"] = "IC number cannot be empty.";
            else if (!_validationService.ValidateICFormat(staff.IC))
                errors["IC"] = "IC number must be in format xxxxxx-xx-xxxx (e.g., 123456-78-9012).";
            else if (await _db.Staffs.AnyAsync(s => s.IC == staff.IC))
                errors["IC"] = "This IC number is already registered.";

            // Validate Email using ValidationService
            if (string.IsNullOrWhiteSpace(staff.Email))
                errors["Email"] = "Email cannot be empty.";
            else if (!_validationService.ValidateEmail(staff.Email))
                errors["Email"] = "Please enter a valid email address.";
            else if (await _db.Staffs.AnyAsync(s => s.Email == staff.Email))
                errors["Email"] = "This email address is already registered.";

            // Format and validate Phone using PhoneService
            string formattedPhoneNumber = _phoneService.FormatPhoneNumber(staff.Phone);

            if (string.IsNullOrWhiteSpace(staff.Phone))
                errors["Phone"] = "Phone number cannot be empty.";
            else if (!_phoneService.ValidatePhoneFormat(formattedPhoneNumber))
                errors["Phone"] = "Phone number must be in format 01X-XXXXXXX or 01X-XXXXXXXX (e.g., 012-1234567).";
            else if (await _db.Staffs.AnyAsync(s => s.Phone == formattedPhoneNumber))
                errors["Phone"] = "This phone number is already registered.";

            // Validate Experience Year using ValidationService
            if (!_validationService.ValidateExperienceYear(staff.ExperienceYear))
                errors["ExperienceYear"] = "Experience must be between 0-50 years.";

            // Validate Position using ValidationService
            if (string.IsNullOrWhiteSpace(staff.Position))
                errors["Position"] = "Please select a position.";
            else if (!_validationService.ValidatePosition(staff.Position))
                errors["Position"] = "Please select a valid position.";

            // If there are validation errors, return them
            if (errors.Any())
            {
                return Json(new { success = false, errors = errors });
            }

            // Update phone number to formatted version
            staff.Phone = formattedPhoneNumber;

            // ========== SMART ID GENERATION ==========
            var allStaffIds = await _db.Staffs
       .Select(s => s.UserId)
                 .OrderBy(id => id)
                 .ToListAsync();

            string newStaffId;

            if (!allStaffIds.Any())
            {
                newStaffId = "S001";
            }
            else
            {
                var usedNumbers = allStaffIds
                .Select(id => int.Parse(id.Substring(1)))
                          .OrderBy(n => n)
             .ToList();

                int nextNumber = 1;
                bool foundGap = false;

                foreach (var num in usedNumbers)
                {
                    if (num != nextNumber)
                    {
                        foundGap = true;
                        break;
                    }
                    nextNumber++;
                }

                if (!foundGap)
                {
                    nextNumber = usedNumbers.Max() + 1;
                }

                newStaffId = "S" + nextNumber.ToString("D3");
            }

            // Get Admin ID from session
            string currentAdminId = HttpContext.Session.GetString("AdminId");

            if (string.IsNullOrEmpty(currentAdminId))
            {
                return Json(new { success = false, errors = new { General = "Admin not logged in. Please login again." } });
            }

            // ========== GENERATE RANDOM PASSWORD USING PASSWORD SERVICE ==========
            string temporaryPassword = _passwordService.GenerateRandomPassword(12);

            // ========== HANDLE PHOTO UPLOAD ==========
            if (PhotoUpload != null && PhotoUpload.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(PhotoUpload.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PhotoUpload.CopyToAsync(stream);
                }

                staff.Photo = "/uploads/" + fileName;
            }
            else
            {
                staff.Photo = "/uploads/placeholder.png";
            }

            // Set all fields
            staff.UserId = newStaffId;
            staff.Role = "staff";
            staff.Password = temporaryPassword;
            staff.CreatedAt = DateTime.Now;
            staff.AdminUserId = currentAdminId;
            staff.Description = staff.Description ?? "";

            // Add to Staffs
            _db.Staffs.Add(staff);
            await _db.SaveChangesAsync();

            // ========== SEND EMAIL WITH CREDENTIALS USING EMAIL SERVICE ==========
            try
            {
                var loginUrl = $"{Request.Scheme}://{Request.Host}/Admin/Auth/Login";
                await _emailService.SendStaffCredentialsEmailAsync(
      toEmail: staff.Email,
           staffName: staff.Name,
           staffId: newStaffId,
      temporaryPassword: temporaryPassword,
     email: staff.Email,
           phone: staff.Phone,
       loginUrl: loginUrl
           );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email send failed for {staff.Email}: {ex.Message}");
            }

            return Json(new
            {
                success = true,
                message = $" Staff {staff.Name} created successfully!",
                staffId = newStaffId
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating groomer: {ex.Message}");
            return Json(new
            {
                success = false,
                errors = new { General = $"Failed to create groomer: {ex.Message}" }
            });
        }
    }
    /// <summary>
    /// AJAX endpoint for editing groomer with validation
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditGroomerAjax(
        [FromForm] Models.Staff staff,
        [FromForm] string editStaffId,
        [FromForm] IFormFile PhotoUpload)
    {
        try
        {
            if (string.IsNullOrEmpty(editStaffId))
            {
                return Json(new { success = false, errors = new Dictionary<string, string> { { "General", "Staff ID is required." } } });
            }

            var dbStaff = await _db.Staffs.FindAsync(editStaffId);
            if (dbStaff == null)
            {
                return Json(new { success = false, errors = new Dictionary<string, string> { { "General", "Staff not found." } } });
            }

            // ========== VALIDATE INPUTS USING SERVICES =========
            var errors = new Dictionary<string, string>();

            // Validate Name
            if (!_validationService.ValidateName(staff.Name))
            {
                if (string.IsNullOrWhiteSpace(staff.Name))
                    errors["Name"] = "Full name cannot be empty.";
                else if (staff.Name.Length < 3 || staff.Name.Length > 200)
                    errors["Name"] = "Name must be between 3-200 characters.";
                else
                    errors["Name"] = "Name must contain only letters and spaces.";
            }

            // Validate IC
            if (string.IsNullOrWhiteSpace(staff.IC))
                errors["IC"] = "IC number cannot be empty.";
            else if (!_validationService.ValidateICFormat(staff.IC))
                errors["IC"] = "IC number must be in format xxxxxx-xx-xxxx (e.g., 123456-78-9012).";
            else if (await _db.Staffs.AnyAsync(s => s.IC == staff.IC && s.UserId != editStaffId))
                errors["IC"] = "This IC number is already registered.";

            // Validate Email
            if (string.IsNullOrWhiteSpace(staff.Email))
                errors["Email"] = "Email cannot be empty.";
            else if (!_validationService.ValidateEmail(staff.Email))
                errors["Email"] = "Please enter a valid email address.";
            else if (await _db.Staffs.AnyAsync(s => s.Email == staff.Email && s.UserId != editStaffId))
                errors["Email"] = "This email address is already registered.";

            // Format and validate Phone
            string formattedPhoneNumber = _phoneService.FormatPhoneNumber(staff.Phone);

            if (string.IsNullOrWhiteSpace(staff.Phone))
                errors["Phone"] = "Phone number cannot be empty.";
            else if (!_phoneService.ValidatePhoneFormat(formattedPhoneNumber))
                errors["Phone"] = "Phone number must be in format 01X-XXXXXXX or 01X-XXXXXXXX (e.g., 012-1234567).";
            else if (await _db.Staffs.AnyAsync(s => s.Phone == formattedPhoneNumber && s.UserId != editStaffId))
                errors["Phone"] = "This phone number is already registered.";

            // Validate Experience Year
            if (staff.ExperienceYear.HasValue && !_validationService.ValidateExperienceYear(staff.ExperienceYear))
                errors["ExperienceYear"] = "Experience must be between 0-50 years.";

            // Validate Position
            if (string.IsNullOrWhiteSpace(staff.Position))
                errors["Position"] = "Please select a position.";
            else if (!_validationService.ValidatePosition(staff.Position))
                errors["Position"] = "Please select a valid position.";

            // If there are validation errors, return them
            if (errors.Any())
            {
                return Json(new { success = false, errors = errors });
            }

            // Update phone number to formatted version
            staff.Phone = formattedPhoneNumber;

            // Update fields
            dbStaff.Name = staff.Name;
            dbStaff.Email = staff.Email;
            dbStaff.Phone = staff.Phone;
            dbStaff.IC = staff.IC;
            dbStaff.Description = staff.Description ?? "";
            dbStaff.ExperienceYear = staff.ExperienceYear;
            dbStaff.Position = staff.Position;

            // Handle photo upload
            if (PhotoUpload != null && PhotoUpload.Length > 0)
            {
                // Validate file size (max 5MB)
                if (PhotoUpload.Length > 5 * 1024 * 1024)
                {
                    return Json(new { success = false, errors = new Dictionary<string, string> { { "Photo", "Photo file size must not exceed 5MB." } } });
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(PhotoUpload.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Json(new { success = false, errors = new Dictionary<string, string> { { "Photo", "Only image files (JPG, JPEG, PNG, GIF) are allowed." } } });
                }

                var fileName = Guid.NewGuid() + fileExtension;
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await PhotoUpload.CopyToAsync(stream);
                }

                // Delete old photo if exists (except placeholder)
                if (!string.IsNullOrEmpty(dbStaff.Photo) && dbStaff.Photo != "/uploads/placeholder.png")
                {
                    var oldPhotoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", dbStaff.Photo.TrimStart('/'));
                    if (System.IO.File.Exists(oldPhotoPath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldPhotoPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WARNING] Failed to delete old photo: {ex.Message}");
                        }
                    }
                }

                dbStaff.Photo = "/uploads/" + fileName;
            }

            await _db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $" Groomer '{staff.Name}' updated successfully!",
                staffId = editStaffId
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error editing groomer: {ex.Message}");
            return Json(new
            {
                success = false,
                errors = new Dictionary<string, string> { { "General", $"Failed to update groomer: {ex.Message}" } }
            });
        }
    }

    /// <summary>
    /// AJAX endpoint to check if field value already exists
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckDuplicateField(string field, string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(value))
            {
                return Json(new { isDuplicate = false });
            }

            bool isDuplicate = false;

            switch (field.ToLower())
            {
                case "ic":
                    isDuplicate = await _db.Staffs.AnyAsync(s => s.IC == value);
                    break;

                case "email":
                    isDuplicate = await _db.Staffs.AnyAsync(s => s.Email == value);
                    break;

                case "phone":
                    // Format phone number before checking using PhoneService
                    string formattedPhone = _phoneService.FormatPhoneNumber(value);
                    isDuplicate = await _db.Staffs.AnyAsync(s => s.Phone == formattedPhone);
                    break;

                default:
                    isDuplicate = false;
                    break;
            }

            return Json(new { isDuplicate = isDuplicate });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking duplicate field: {ex.Message}");
            return Json(new { isDuplicate = false });
        }
    }

    /// <summary>
    /// AJAX endpoint to check if field value already exists (excluding specific user)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckDuplicateFieldForEdit(string field, string value, string excludeUserId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(value))
            {
                return Json(new { isDuplicate = false });
            }

            bool isDuplicate = false;

            switch (field.ToLower())
            {
                case "ic":
                    isDuplicate = await _db.Staffs.AnyAsync(s => s.IC == value && s.UserId != excludeUserId);
                    break;

                case "email":
                    isDuplicate = await _db.Staffs.AnyAsync(s => s.Email == value && s.UserId != excludeUserId);
                    break;

                case "phone":
                    // Format phone number before checking using PhoneService
                    string formattedPhone = _phoneService.FormatPhoneNumber(value);
                    isDuplicate = await _db.Staffs.AnyAsync(s => s.Phone == formattedPhone && s.UserId != excludeUserId);
                    break;

                default:
                    isDuplicate = false;
                    break;
            }

            return Json(new { isDuplicate = isDuplicate });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking duplicate field: {ex.Message}");
            return Json(new { isDuplicate = false });
        }
    }

    // ========== CLEAR SERVICES TABLE ==========
    // POST: Clear all services and service-category links (Admin only)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearServices()
    {
        try
        {
            // Remove junction rows first
            var allJunctions = _db.ServiceServiceCategories.ToList();
            if (allJunctions.Any())
            {
                _db.ServiceServiceCategories.RemoveRange(allJunctions);
            }

            // Then remove services
            var allServices = _db.Services.ToList();
            if (allServices.Any())
            {
                _db.Services.RemoveRange(allServices);
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "All services and service-category links cleared.";
        }
        catch (Exception ex)
        {
            // Log and show error
            Console.WriteLine($"ClearServices failed: {ex.Message}");
            TempData["ErrorMessage"] = "Failed to clear service tables: " + ex.Message;
        }

        return RedirectToAction(nameof(Service));
    }

    // AJAX: Delete a single Service and its ServiceServiceCategories
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteServiceAjax([FromForm] string serviceId)
    {
        try
        {
            if (string.IsNullOrEmpty(serviceId))
                return Json(new { success = false, message = "Invalid service id." });

            var dbService = await _db.Services
            .Include(s => s.ServiceServiceCategories)
            .FirstOrDefaultAsync(s => s.ServiceId == serviceId);

            if (dbService == null)
                return Json(new { success = false, message = "Service not found." });

            // Remove junction rows first
            if (dbService.ServiceServiceCategories != null && dbService.ServiceServiceCategories.Any())
            {
                _db.ServiceServiceCategories.RemoveRange(dbService.ServiceServiceCategories);
            }

            _db.Services.Remove(dbService);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Service deleted successfully.", serviceId = serviceId });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DeleteServiceAjax failed: {ex.Message}");
            return Json(new { success = false, message = "Failed to delete service." });
        }
    }

}
