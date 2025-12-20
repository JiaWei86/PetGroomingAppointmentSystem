using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PetGroomingAppointmentSystem.Areas.Admin.Controllers;
using PetGroomingAppointmentSystem.Areas.Admin.Services;
using PetGroomingAppointmentSystem.Areas.Customer.ViewModels;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Models.ViewModels;
using PetGroomingAppointmentSystem.Services;
using AdminServices = PetGroomingAppointmentSystem.Areas.Admin.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;

namespace PetGroomingAppointmentSystem.Areas.Admin.Controllers;

[Area("Admin")]
[AdminOnly]
public class HomeController : Controller
{
    private readonly DB _db;
    private readonly AdminServices.IEmailService _emailService;  // Use alias
    private readonly AdminServices.IPasswordService _passwordService;  // Use alias
    private readonly AdminServices.IPhoneService _phoneService;  // Use alias
    private readonly AdminServices.IValidationService _validationService;  // Use alias
    private readonly IS3StorageService _s3Service;

    public HomeController(
        DB context,
        AdminServices.IEmailService emailService,  // Use alias
        AdminServices.IPasswordService passwordService,  // Use alias
        AdminServices.IPhoneService phoneService,  // Use alias
        AdminServices.IValidationService validationService,  // Use alias
        IS3StorageService s3Service)
    {
        _db = context;
        _emailService = emailService;
        _passwordService = passwordService;
        _phoneService = phoneService;
        _validationService = validationService;
        _s3Service = s3Service;
    }
    public class FieldValidationRequest
    {
        public required string StaffId { get; set; }
        public required string FieldName { get; set; }
        public required string IcValue { get; set; } // 新增：用于传递IC号码
        public required string FieldValue { get; set; }
    }


    // ========== DASHBOARD
    public async Task<IActionResult> Index()
    {
        ViewData["ActivePage"] = "Dashboard";

        var viewModel = new DashboardViewModel();
        var now = DateTime.Now;

        // --- 1. 填充统计卡片 (Stat Cards) ---

        // 总预约数 (本月 vs 上月)
        var firstDayOfCurrentMonth = new DateTime(now.Year, now.Month, 1);
        var firstDayOfLastMonth = firstDayOfCurrentMonth.AddMonths(-1);
        var lastDayOfLastMonth = firstDayOfCurrentMonth.AddDays(-1);

        int currentMonthAppointments = await _db.Appointments
        .CountAsync(a => a.AppointmentDateTime >= firstDayOfCurrentMonth && a.AppointmentDateTime < firstDayOfCurrentMonth.AddMonths(1));

        int lastMonthAppointments = await _db.Appointments
        .CountAsync(a => a.AppointmentDateTime >= firstDayOfLastMonth && a.AppointmentDateTime < firstDayOfCurrentMonth);

        viewModel.TotalAppointments.Count = currentMonthAppointments;
        if (lastMonthAppointments > 0)
        {
            viewModel.TotalAppointments.ChangePercentage = Math.Round(((decimal)(currentMonthAppointments - lastMonthAppointments) / lastMonthAppointments) * 100, 2);
        }
        else if (currentMonthAppointments > 0)
        {
            viewModel.TotalAppointments.ChangePercentage = 100; // 如果上月为0，本月有，则增长100%
        }

        // 活跃美容师数量
        viewModel.ActiveGroomers.Count = await _db.Staffs.CountAsync(s => s.Role == "staff");

        // 总顾客数量 (替换了原来的"待处理预约")
        viewModel.PendingAppointments.Count = await _db.Customers.CountAsync();


        // --- 2. 塡充忠诚度积分 (Loyalty Points) ---
        // 注意: 以下是基于现有模型的模拟。您需要根据您的业务逻辑调整。
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7);

        // 假设每次完成的预约都奖励10个积分
        viewModel.LoyaltyPoints.AwardedThisWeek = await _db.Appointments
        .CountAsync(a => a.Status == "Completed" && a.AppointmentDateTime >= startOfWeek && a.AppointmentDateTime < endOfWeek) * 10;

        // 活跃会员数 (假设为总顾客数)
        viewModel.LoyaltyPoints.ActiveMembers = await _db.Customers.CountAsync();

        // 假设每次使用礼品兑换都消耗500积分
        viewModel.LoyaltyPoints.RedeemedThisWeek = 0; // 您需要一个礼品兑换记录表来计算这个值
 {
 ViewData["ActivePage"] = "Dashboard";

 var viewModel = new DashboardViewModel();
 var now = DateTime.Now;

 // --- 1. 填充统计卡片 (Stat Cards) ---

 // 总预约数 (本月 vs 上月)
 var firstDayOfCurrentMonth = new DateTime(now.Year, now.Month, 1);
 var firstDayOfLastMonth = firstDayOfCurrentMonth.AddMonths(-1);
 var lastDayOfLastMonth = firstDayOfCurrentMonth.AddDays(-1);

 int currentMonthAppointments = await _db.Appointments
 .CountAsync(a => a.AppointmentDateTime >= firstDayOfCurrentMonth && a.AppointmentDateTime < firstDayOfCurrentMonth.AddMonths(1));

 int lastMonthAppointments = await _db.Appointments
 .CountAsync(a => a.AppointmentDateTime >= firstDayOfLastMonth && a.AppointmentDateTime < firstDayOfCurrentMonth);

 viewModel.TotalAppointments.Count = currentMonthAppointments;
 if (lastMonthAppointments > 0)
 {
 viewModel.TotalAppointments.ChangePercentage = Math.Round(((decimal)(currentMonthAppointments - lastMonthAppointments) / lastMonthAppointments) * 100, 2);
 }
 else if (currentMonthAppointments > 0)
 {
 viewModel.TotalAppointments.ChangePercentage = 100; // 如果上月为0，本月有，则增长100%
 }

 // 活跃美容师数量
 viewModel.ActiveGroomers.Count = await _db.Staffs.CountAsync(s => s.Role == "staff");

 // 总顾客数量 (替换了原来的"待处理预约")
 viewModel.PendingAppointments.Count = await _db.Customers.CountAsync();


        // --- 2. 填充忠诚度积分 (Loyalty Points) ---
        // Note: Calculate based on actual customer loyalty point activities
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7);

        // Total points awarded this week (sum of completed appointments * 10 points each)
        viewModel.LoyaltyPoints.RedeemedThisWeek = await _db.CustomerRedeemGifts
            .Where(crg => crg.RedeemDate >= startOfWeek && crg.RedeemDate < endOfWeek)
            .Include(crg => crg.Gift)
            .SumAsync(crg => crg.Gift.LoyaltyPointCost * crg.QuantityRedeemed);

        // Active members count (customers with active status)
        viewModel.LoyaltyPoints.ActiveMembers = await _db.Customers
            .CountAsync(c => c.Status == "active");



        // --- 3. 填充图表数据 (Chart Data) ---

        // 周视图 (过去7天)
        var last7Days = Enumerable.Range(0, 7).Select(i => now.AddDays(-i).Date).Reverse().ToList();
        var weeklyData = await _db.Appointments
        .Where(a => a.AppointmentDateTime.HasValue && a.AppointmentDateTime.Value.Date >= last7Days.First() && a.AppointmentDateTime.Value.Date <= last7Days.Last())
        .GroupBy(a => a.AppointmentDateTime!.Value.Date)
        .Select(g => new { Date = g.Key, Count = g.Count() })
        .ToListAsync();

        var weeklyDict = weeklyData.ToDictionary(x => x.Date, x => x.Count);
        viewModel.ChartData.Week = new ChartSeriesModel
        {
            Labels = last7Days.Select(d => d.ToString("ddd")).ToList(),
            Data = last7Days.Select(d => weeklyDict.ContainsKey(d) ? weeklyDict[d] : 0).ToList()
        };

        // 月视图 (本月按周)
        var weeksInMonth = Enumerable.Range(0, 4)
        .Select(i => $"Week {i + 1}")
        .ToList();
        var monthData = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            var weekStart = firstDayOfCurrentMonth.AddDays(i * 7);
            var weekEnd = weekStart.AddDays(7);
            int weekCount = await _db.Appointments
            .CountAsync(a => a.AppointmentDateTime >= weekStart && a.AppointmentDateTime < weekEnd);
            monthData.Add(weekCount);
        }
        viewModel.ChartData.Month = new ChartSeriesModel { Labels = weeksInMonth, Data = monthData };

        // 日视图 (今天按小时)
        var todayStart = now.Date.AddHours(9); // 9 AM
        var todayEnd = now.Date.AddHours(17); // 5 PM
        var hourlyData = await _db.Appointments
        .Where(a => a.AppointmentDateTime >= todayStart && a.AppointmentDateTime < todayEnd)
        .GroupBy(a => a.AppointmentDateTime.Value.Hour)
        .Select(g => new { Hour = g.Key, Count = g.Count() })
        .ToListAsync();

        var hourlyDict = hourlyData.ToDictionary(x => x.Hour, x => x.Count);
        var dayLabels = new List<string>();
        var dayData = new List<int>();
        for (int hour = 9; hour <= 16; hour++)
        {
            dayLabels.Add(new DateTime(1, 1, 1, hour, 0, 0).ToString("h tt"));
            dayData.Add(hourlyDict.ContainsKey(hour) ? hourlyDict[hour] : 0);
        }
        viewModel.ChartData.Day = new ChartSeriesModel { Labels = dayLabels, Data = dayData };


        // --- 4. 塡充日历预约 (Calendar Appointments) ---
        var calendarStartDate = firstDayOfCurrentMonth.AddMonths(-1);
        var calendarEndDate = firstDayOfCurrentMonth.AddMonths(2);

        viewModel.AppointmentsForCalendar = await _db.Appointments
        .Where(a => a.AppointmentDateTime >= calendarStartDate && a.AppointmentDateTime < calendarEndDate)
        .Include(a => a.Pet)
        .Include(a => a.Staff)
        .Include(a => a.Service)
        .Select(a => new CalendarAppointmentModel
        {
            Id = a.AppointmentId,
            Date = a.AppointmentDateTime.Value.ToString("yyyy-MM-dd"),
            Time = a.AppointmentDateTime.Value.ToString("HH:mm"),
            PetName = a.Pet.Name,
            GroomerName = a.Staff.Name,
            ServiceType = a.Service.Name,
            Status = a.Status.ToLower()
        })
        .ToListAsync();


        return View(viewModel);
    }
 var weeklyData = await _db.Appointments
 .Where(a => a.AppointmentDateTime.HasValue && a.AppointmentDateTime.Value.Date >= last7Days.First() && a.AppointmentDateTime.Value.Date <= last7Days.Last())
 .GroupBy(a => a.AppointmentDateTime.Value.Date)
 .Select(g => new { Date = g.Key, Count = g.Count() })
 .ToListAsync();

 var weeklyDict = weeklyData.ToDictionary(x => x.Date, x => x.Count);
 viewModel.ChartData.Week = new ChartSeriesModel
 {
 Labels = last7Days.Select(d => d.ToString("ddd")).ToList(),
 Data = last7Days.Select(d => weeklyDict.ContainsKey(d) ? weeklyDict[d] : 0).ToList()
 };

 // 月视图 (本月按周)
 var weeksInMonth = Enumerable.Range(0, 4)
 .Select(i => $"Week {i + 1}")
 .ToList();
 var monthData = new List<int>();
 for (int i = 0; i < 4; i++)
 {
 var weekStart = firstDayOfCurrentMonth.AddDays(i * 7);
 var weekEnd = weekStart.AddDays(7);
 int weekCount = await _db.Appointments
 .CountAsync(a => a.AppointmentDateTime >= weekStart && a.AppointmentDateTime < weekEnd);
 monthData.Add(weekCount);
 }
 viewModel.ChartData.Month = new ChartSeriesModel { Labels = weeksInMonth, Data = monthData };

 // 日视图 (今天按小时)
 var todayStart = now.Date.AddHours(9); // 9 AM
 var todayEnd = now.Date.AddHours(17); // 5 PM
 var hourlyData = await _db.Appointments
 .Where(a => a.AppointmentDateTime >= todayStart && a.AppointmentDateTime < todayEnd)
 .GroupBy(a => a.AppointmentDateTime.Value.Hour)
 .Select(g => new { Hour = g.Key, Count = g.Count() })
 .ToListAsync();

 var hourlyDict = hourlyData.ToDictionary(x => x.Hour, x => x.Count);
 var dayLabels = new List<string>();
 var dayData = new List<int>();
 for (int hour = 9; hour <= 16; hour++)
 {
 dayLabels.Add(new DateTime(1, 1, 1, hour, 0, 0).ToString("h tt"));
 dayData.Add(hourlyDict.ContainsKey(hour) ? hourlyDict[hour] : 0);
 }
 viewModel.ChartData.Day = new ChartSeriesModel { Labels = dayLabels, Data = dayData };


 // --- 4. 填充日历预约 (Calendar Appointments) ---
 var calendarStartDate = firstDayOfCurrentMonth.AddMonths(-1);
 var calendarEndDate = firstDayOfCurrentMonth.AddMonths(2);

 viewModel.AppointmentsForCalendar = await _db.Appointments
 .Where(a => a.AppointmentDateTime >= calendarStartDate && a.AppointmentDateTime < calendarEndDate)
 .Include(a => a.Pet)
 .Include(a => a.Staff)
 .Include(a => a.Service)
 .Select(a => new CalendarAppointmentModel
 {
 Id = a.AppointmentId,
 Date = a.AppointmentDateTime.Value.ToString("yyyy-MM-dd"),
 Time = a.AppointmentDateTime.Value.ToString("HH:mm"),
 PetName = a.Pet.Name,
 GroomerName = a.Staff.Name,
 ServiceType = a.Service.Name,
 Status = a.Status.ToLower()
 })
 .ToListAsync();


 return View(viewModel);
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

            var fieldNameNormalized = request.FieldName?.Trim().ToLowerInvariant() ?? string.Empty;
            switch (fieldNameNormalized)
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
                    // If StaffId is provided (edit mode), exclude it from the check.
                    isDuplicate = string.IsNullOrEmpty(request.StaffId)
                        ? await _db.Staffs.AnyAsync(s => s.IC == valueToCheck)
                        : await _db.Staffs.AnyAsync(s => s.IC == valueToCheck && s.UserId != request.StaffId);

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
                    // If StaffId is provided (edit mode), exclude it from the check.
                    isDuplicate = string.IsNullOrEmpty(request.StaffId)
                        ? await _db.Staffs.AnyAsync(s => s.Email.ToLower() == valueToCheck.ToLower())
                        : await _db.Staffs.AnyAsync(s => s.Email.ToLower() == valueToCheck.ToLower() && s.UserId != request.StaffId);

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
                    // If StaffId is provided (edit mode), exclude it from the check.
                    isDuplicate = string.IsNullOrEmpty(request.StaffId)
                        ? await _db.Staffs.AnyAsync(s => s.Phone == formattedPhone)
                        : await _db.Staffs.AnyAsync(s => s.Phone == formattedPhone && s.UserId != request.StaffId);

                    if (isDuplicate)
                    {
                        errorMessage = "This phone number is already registered.";
                    }
                    break;

                case "experienceyear":
                case "experience":
                    if (!int.TryParse(valueToCheck, out int expVal))
                    {
                        return Json(new { isValid = false, errorMessage = "Experience must be a number." });
                    }
                    if (!_validationService.ValidateExperienceYear(expVal))
                    {
                        return Json(new { isValid = false, errorMessage = "Experience must be between0-50 years." });
                    }

                    if (!string.IsNullOrEmpty(request.IcValue))
                    {
                        var validationResult = _validationService.ValidateExperienceAgainstAge(expVal, request.IcValue);
                        if (!validationResult.IsValid)
                            return Json(new { isValid = false, errorMessage = validationResult.ErrorMessage });
                    }
                    break;

                case "description":
                case "desc":
                    // optional, but enforce reasonable max length
                    if (!string.IsNullOrEmpty(valueToCheck) && valueToCheck.Length > 500)
                    {
                        return Json(new { isValid = false, errorMessage = "Description must be500 characters or fewer." });
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
            var validationErrors = await ValidateStaffAsync(staff);

            if (validationErrors.Any())
            {
                TempData["ErrorMessage"] = string.Join("\n", validationErrors.Values);
                return RedirectToAction(nameof(Groomer));
            }

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
                TempData["ErrorMessage"] = "Admin not logged in. Please login again.";
                return RedirectToAction("Login", "Auth");
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
                    staff.Email,
                    staff.Name,
                    newStaffId,
                    temporaryPassword,
                    staff.Email,
                    staff.Phone,
                    loginUrl
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

            // ========== VALIDATE INPUTS USING VALIDATION SERVICE =========
            var validationErrors = await ValidateStaffAsync(staff, editStaffId);

            if (validationErrors.Any())
            {
                TempData["ErrorMessage"] = string.Join("\n", validationErrors.Values);
                return RedirectToAction(nameof(Groomer), new { editId = editStaffId });
            }

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
    // ========== CUSTOMER MANAGEMENT ==========
    // GET: List all customers with filter
    public async Task<IActionResult> Customer(string searchName, string searchEmail, string searchStatus)
    {
        ViewData["ActivePage"] = "Customer";

        var query = _db.Customers.AsQueryable();

        if (!string.IsNullOrEmpty(searchName))
        {
            query = query.Where(c => c.Name.Contains(searchName));
        }

        if (!string.IsNullOrEmpty(searchEmail))
        {
            query = query.Where(c => c.Email.Contains(searchEmail));
        }

        if (!string.IsNullOrEmpty(searchStatus))
        {
            query = query.Where(c => c.Status == searchStatus);
        }

        var customers = await query
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        // ✅ Manually load all pets for each customer
        var allPets = await _db.Pets.ToListAsync();
        foreach (var customer in customers)
        {
            customer.Pets = allPets
                .Where(p => p.CustomerId == customer.UserId)
                .ToList();
        }

        return View(customers);
    }

    // GET: Customer Profile Page
    public async Task<IActionResult> CustomerProfile(string customerId)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == customerId);

        if (customer == null)
            return NotFound();

        // Load related data
        var pets = await _db.Pets
            .Where(p => p.CustomerId == customerId)
            .ToListAsync();

        var redeems = await _db.CustomerRedeemGifts
            .Where(r => r.CustomerId == customerId)
            .Include(r => r.Gift)
            .ToListAsync();

        customer.Pets = pets;
        customer.Redeems = redeems;

        return View(customer);
    }

    // POST: Create Customer
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCustomer(RegisterViewModel model, IFormFile PhotoUpload)
    {
        // Remove Password validation for admin-created customers (password is auto-generated)
        ModelState.Remove("Password");
        ModelState.Remove("ConfirmPassword");

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            TempData["ErrorMessage"] = string.Join(" ", errors);
            return RedirectToAction(nameof(Customer));
        }

        // Format and validate phone
        string formattedPhone = _phoneService.FormatPhoneNumber(model.PhoneNumber);

        // Check for duplicate phone
        if (await _db.Customers.AnyAsync(c => c.Phone == formattedPhone))
        {
            TempData["ErrorMessage"] = "This phone number is already registered.";
            return RedirectToAction(nameof(Customer));
        }

        // Check for duplicate email
        if (await _db.Customers.AnyAsync(c => c.Email == model.Email))
        {
            TempData["ErrorMessage"] = "This email address is already registered.";
            return RedirectToAction(nameof(Customer));
        }

        // Check for duplicate IC
        if (await _db.Customers.AnyAsync(c => c.IC == model.IC))
        {
            TempData["ErrorMessage"] = "This IC number is already registered.";
            return RedirectToAction(nameof(Customer));
        }

        // ✅ Smart ID Generation
        var allCustomerIds = await _db.Customers
            .Select(c => c.UserId)
            .Where(id => id != null && id.StartsWith("C"))
            .ToListAsync();

        string newCustomerId;

        if (!allCustomerIds.Any())
        {
            newCustomerId = "C001";
        }
        else
        {
            var usedNumbers = allCustomerIds
                .Select(id => int.TryParse(id.Substring(1), out int num) ? num : 0)
                .Where(n => n > 0)
                .OrderBy(n => n)
                .ToList();

            int nextNumber = 1;
            foreach (var num in usedNumbers)
            {
                if (num != nextNumber) break;
                nextNumber++;
            }

            if (nextNumber <= usedNumbers.Max())
                newCustomerId = "C" + nextNumber.ToString("D3");
            else
                newCustomerId = "C" + (usedNumbers.Max() + 1).ToString("D3");
        }

        // ✅ Generate temporary password
        string temporaryPassword = _passwordService.GenerateRandomPassword(12);

        // Create customer from RegisterViewModel
        var customer = new Models.Customer
        {
            UserId = newCustomerId,
            Name = model.Name,
            IC = model.IC,
            Email = model.Email,
            Phone = formattedPhone,
            Password = temporaryPassword,
            Role = "customer",
            Status = "pending_password",
            CreatedAt = DateTime.Now,
            RegisteredDate = DateTime.Now,
            LoyaltyPoint = 0,
            Photo = "/uploads/placeholder.png"
        };

        // ✅ Upload photo to S3 instead of local storage
        if (PhotoUpload != null && PhotoUpload.Length > 0)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await PhotoUpload.CopyToAsync(memoryStream);
                var base64String = Convert.ToBase64String(memoryStream.ToArray());
                var contentType = PhotoUpload.ContentType ?? "image/jpeg";
                var base64Image = $"data:{contentType};base64,{base64String}";

                var cloudFrontUrl = await _s3Service.UploadBase64ImageAsync(
                    base64Image,
                    $"customers/{newCustomerId}"
                );
                customer.Photo = cloudFrontUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading customer photo to S3: {ex.Message}");
                customer.Photo = "/uploads/placeholder.png";
            }
        }

        // Save to database
        try
        {
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }
        catch (Exception dbEx)
        {
            TempData["ErrorMessage"] = $"Failed to create customer: {dbEx.Message}";
            return RedirectToAction(nameof(Customer));
        }

        // Send email with credentials
        try
        {
            var loginUrl = $"{Request.Scheme}://{Request.Host}/Customer/Auth/Login";

            bool emailSent = await _emailService.SendCustomerCredentialsEmailAsync(
                toEmail: customer.Email,
                customerName: customer.Name,
                customerId: newCustomerId,
                temporaryPassword: temporaryPassword,
                phone: customer.Phone,
                loginUrl: loginUrl
            );

            if (emailSent)
                TempData["SuccessMessage"] = $"✅ Customer {customer.Name} created successfully! Credentials sent to {customer.Email}.";
            else
                TempData["SuccessMessage"] = $"✅ Customer {customer.Name} created! ⚠️ Email failed - Temp Password: {temporaryPassword}";
        }
        catch (Exception)
        {
            TempData["SuccessMessage"] = $"✅ Customer {customer.Name} created! ⚠️ Email failed - Temp Password: {temporaryPassword}";
        }

        // ✅ ADD LOYALTY POINTS (10 points per confirmed appointment when admin creates it)
        var appointmentCustomer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == customer.UserId);
        if (appointmentCustomer != null && customer.Status == "Confirmed")
        {
            appointmentCustomer.LoyaltyPoint += 10;
            _db.Customers.Update(appointmentCustomer);
        }

        return RedirectToAction(nameof(Customer));
    }


    // POST: Edit Customer
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCustomer(string customerId, Models.Customer customer, IFormFile PhotoUpload)
    {
        var dbCustomer = await _db.Customers.FindAsync(customerId);

        if (dbCustomer == null)
            return NotFound();

        dbCustomer.Name = customer.Name;
        dbCustomer.Email = customer.Email;
        dbCustomer.Phone = customer.Phone;
        dbCustomer.IC = customer.IC;
        dbCustomer.Status = customer.Status;

        // ✅ Upload new photo to S3 if provided
        if (PhotoUpload != null && PhotoUpload.Length > 0)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await PhotoUpload.CopyToAsync(memoryStream);
                var base64String = Convert.ToBase64String(memoryStream.ToArray());
                var contentType = PhotoUpload.ContentType ?? "image/jpeg";
                var base64Image = $"data:{contentType};base64,{base64String}";

                var cloudFrontUrl = await _s3Service.UploadBase64ImageAsync(
                    base64Image,
                    $"customers/{customerId}"
                );
                dbCustomer.Photo = cloudFrontUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uploading customer photo to S3: {ex.Message}");
                // Keep existing photo if S3 upload fails
            }
        }

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(CustomerProfile), new { customerId = customerId });
    }

    // POST: Delete Customer
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomer(string customerId)
    {
        var dbCustomer = await _db.Customers.FindAsync(customerId);

        if (dbCustomer == null)
            return NotFound();

        _db.Customers.Remove(dbCustomer);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Customer));
    }
    // ========== ADD PET TO CUSTOMER ==========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPetToCustomer(
        string customerId,
        string petName,
        string petType,
        string petBreed,
        int? petAge,
        string petRemark,
        IFormFile petPhotoUpload) // ✅ Add photo parameter
    {
        try
        {
            if (string.IsNullOrEmpty(customerId) || string.IsNullOrEmpty(petName))
            {
                return Json(new { success = false, message = "Customer ID and Pet Name are required" });
            }

            // Generate sequential PetId (P001, P002, etc.)
            int nextNumber = 1;

            var existingPetIds = await _db.Pets
                .Where(p => p.PetId.StartsWith("P"))
                .Select(p => p.PetId)
                .ToListAsync();

            if (existingPetIds.Any())
            {
                // Extract numeric part and find the maximum
                nextNumber = existingPetIds
                    .Select(id => int.TryParse(id.Substring(1), out var n) ? n : 0)
                    .Select(id => int.TryParse(id.Substring(1), out int num) ? num : 0)
                    .Where(n => n > 0)
                    .OrderBy(n => n)
                    .ToList()
                    .Last() + 1;
            }

            var petId = $"P{nextNumber:D3}";
            string? photoPath = null;

            // ✅ Upload photo to S3 if provided
            if (petPhotoUpload != null && petPhotoUpload.Length > 0)
            {
                try
                {
                    using var memoryStream = new MemoryStream();
                    await petPhotoUpload.CopyToAsync(memoryStream);
                    var base64String = Convert.ToBase64String(memoryStream.ToArray());
                    var contentType = petPhotoUpload.ContentType ?? "image/jpeg";
                    var base64Image = $"data:{contentType};base64,{base64String}";

                    var cloudFrontUrl = await _s3Service.UploadBase64ImageAsync(
                        base64Image,
                        $"pets/{petId}"
                    );
                    photoPath = cloudFrontUrl;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading pet photo to S3: {ex.Message}");
                }
            }

            var pet = new Pet
            {
                PetId = petId,
                Name = petName,
                Type = petType,
                Breed = petBreed,
                Age = petAge,
                Remark = petRemark,
                CustomerId = customerId,
                Photo = photoPath ?? "/images/pet-placeholder.png"
            };

            _db.Pets.Add(pet);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Pet added successfully", petId = petId, photo = pet.Photo });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ========== DELETE PET ==========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePet(string petId)
    {
        try
        {
            var pet = await _db.Pets.FindAsync(petId);
            if (pet == null)
            {
                return Json(new { success = false, message = "Pet not found" });
            }

            _db.Pets.Remove(pet);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Pet deleted successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ========== ADJUST LOYALTY POINTS ==========
    [HttpPost]
    public async Task<IActionResult> AdjustLoyaltyPoints(string customerId, string adjustmentType, int amount)
    {
        try
        {
            var customer = await _db.Customers.FindAsync(customerId);
            if (customer == null)
            {
                return Json(new { success = false, message = "Customer not found" });
            }

            int oldBalance = customer.LoyaltyPoint;
            int newBalance = oldBalance;

            if (adjustmentType == "add")
            {
                newBalance = oldBalance + amount;
            }
            else if (adjustmentType == "subtract")
            {
                newBalance = Math.Max(0, oldBalance - amount);
            }

            customer.LoyaltyPoint = newBalance;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = $"Points updated from {oldBalance} to {newBalance}", newBalance = newBalance });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    // ========== UPDATE PET ==========
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePet(
        string petId,
        string petName,
        string petType,
        string petBreed,
        int? petAge,
        string petRemark,
        IFormFile petPhotoUpload) // ✅ Add photo parameter
    {
        try
        {
            if (string.IsNullOrEmpty(petId) || string.IsNullOrEmpty(petName))
            {
                return Json(new { success = false, message = "Pet ID and Pet Name are required" });
            }

            var pet = await _db.Pets.FindAsync(petId);
            if (pet == null)
            {
                return Json(new { success = false, message = "Pet not found" });
            }

            pet.Name = petName;
            pet.Type = petType;
            pet.Breed = petBreed;
            pet.Age = petAge;
            pet.Remark = petRemark;

            // ✅ Upload new photo to S3 if provided
            if (petPhotoUpload != null && petPhotoUpload.Length > 0)
            {
                try
                {
                    using var memoryStream = new MemoryStream();
                    await petPhotoUpload.CopyToAsync(memoryStream);
                    var base64String = Convert.ToBase64String(memoryStream.ToArray());
                    var contentType = petPhotoUpload.ContentType ?? "image/jpeg";
                    var base64Image = $"data:{contentType};base64,{base64String}";

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

            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Pet updated successfully", photo = pet.Photo });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }



    // ========== APPOINTMENT ==========
    public async Task<IActionResult> Appointment(string status, string groomerid, string date, string editId, string filterAppointmentId, string filterCustomerName)
    {
        ViewData["ActivePage"] = "Appointment";
        // Base query
        var query = _db.Appointments
        .Include(a => a.Customer)
        .Include(a => a.Pet)
        .Include(a => a.Staff)
        .Include(a => a.Service)
        .AsQueryable();

        // Apply Appointment ID filter
        if (!string.IsNullOrEmpty(filterAppointmentId))
        {
            query = query.Where(a => a.AppointmentId.Contains(filterAppointmentId));
        }

        // Apply Customer Name filter
        if (!string.IsNullOrEmpty(filterCustomerName))
        {
            query = query.Where(a => a.Customer.Name.Contains(filterCustomerName));
        }

        // Apply status filter if it's not "All"
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrEmpty(groomerid))
        {
            query = query.Where(a => a.StaffId == groomerid);
        }

        if (!string.IsNullOrEmpty(date))
        {
            if (DateTime.TryParse(date, out var parsedDate))
            {
                var start = parsedDate.Date;
                var end = start.AddDays(1);
                query = query.Where(a => a.AppointmentDateTime >= start && a.AppointmentDateTime < end);
            }
        }

        var appointments = await query.OrderByDescending(a => a.AppointmentDateTime).ToListAsync();

        // Create and populate the ViewModel
        var viewModel = new AppointmentViewModel
        {
            Appointments = appointments,
            FilterStatus = status,
            FilterGroomerId = groomerid,
            FilterDate = DateTime.TryParse(date, out var parsedFilterDate) ? parsedFilterDate : (DateTime?)null,
            FilterAppointmentId = filterAppointmentId,
            FilterCustomerName = filterCustomerName
        };

        // Load lookup data for the ViewModel
        viewModel.StaffList = await _db.Staffs
            .Select(s => new SelectListItem { Value = s.UserId, Text = s.Name })
            .ToListAsync();

        viewModel.CustomerList = await _db.Customers
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.UserId, Text = c.Name + " (" + c.Phone + ")" })
            .ToListAsync();

        // If a customer is pre-selected (e.g., due to validation error), load their pets
        if (!string.IsNullOrEmpty(viewModel.CustomerId))
        {
            viewModel.PetList = await _db.Pets
                .Where(p => p.CustomerId == viewModel.CustomerId)
                .Select(p => new SelectListItem { Value = p.PetId, Text = p.Name })
                .ToListAsync();
        }

        ViewBag.EditingId = editId;

        return View(viewModel);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Appointment(AppointmentViewModel model, string actionType)
    {
        if (actionType == "create")
        {
            // 1. 检查模型状态是否有效
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                                    .SelectMany(v => v.Errors)
                                    .Select(e => e.ErrorMessage)
                                    .ToList();
                TempData["ErrorMessage"] = "Data is invalid. Please check your inputs. Details: " + string.Join("; ", errors);
                return RedirectToAction(nameof(Appointment));
            }

            // 2. 检查宠物和服务的数量是否匹配
            if (model.PetId == null || model.PetServiceMap == null || model.PetId.Count == 0 || model.PetId.Count != model.PetServiceMap.Count)
            {
                TempData["ErrorMessage"] = "An error occurred. The number of selected pets must match the number of selected services. Please try again.";
                return RedirectToAction(nameof(Appointment));
            }

            try
            {
                // ========== 1. INITIAL VALIDATION ==========

                // Validate Admin Session
                string? currentAdminId = HttpContext.Session.GetString("AdminId");
                if (string.IsNullOrEmpty(currentAdminId))
                {
                    TempData["ErrorMessage"] = "Your session has expired. Please log in again.";
                    return RedirectToAction("Login", "Auth", new { area = "Admin" });
                }

                // Validate Customer Status
                var customer = await _db.Customers.FindAsync(model.CustomerId);
                if (customer == null || string.IsNullOrWhiteSpace(customer.Status) || customer.Status.Trim().ToUpperInvariant() != "ACTIVE")
                {
                    TempData["ErrorMessage"] = "Appointments can only be created for 'Active' customers.";
                    return RedirectToAction(nameof(Appointment));
                }

                // Validate Appointment Time
                if (model.AppointmentDateTime < DateTime.Now)
                {
                    TempData["ErrorMessage"] = "You cannot create an appointment for a time that has already passed.";
                    return RedirectToAction(nameof(Appointment));
                }

                // ========== 2. SETUP FOR APPOINTMENT CREATION ==========

                var newAppointments = new List<Appointment>();
                var appointmentProcessingTime = model.AppointmentDateTime;

                // Prepare for Smart ID Generation
                var allAppointmentIds = await _db.Appointments.Select(a => a.AppointmentId).ToListAsync();
                var usedNumbers = allAppointmentIds
                    .Where(id => !string.IsNullOrEmpty(id) && id.StartsWith("AP"))
                    .Select(id => int.TryParse(id.Substring(2), out var n) ? n : -1)
                    .Where(n => n > 0)
                    .ToHashSet();
                int nextIdNum = 1;

                TempData["DebugMessage"] = "";

                // Create a dictionary to hold pet-specific data
                var petTasks = new Dictionary<string, (Service service, string groomerId)>();

                // ========== 3. PRE-PROCESSING & VALIDATION FOR ALL PETS ==========

                if (model.GroomerMode == "one_sequential")
                {
                    if (string.IsNullOrEmpty(model.StaffId) || model.StaffId == "any")
                    {
                        TempData["ErrorMessage"] = "You must select a specific groomer for the 'One Groomer (Sequential)' mode.";
                        return RedirectToAction(nameof(Appointment));
                    }

                    foreach (var petId in model.PetId)
                    {
                        if (model.PetServiceMap.TryGetValue(petId, out var serviceId))
                        {
                            var service = await _db.Services.FindAsync(serviceId);
                            if (service != null && service.DurationTime.HasValue)
                            {
                                petTasks[petId] = (service, model.StaffId);
                            }
                        }
                    }
                }
                else
                {
                    List<Models.Staff> availableGroomersForAutoAssign = null;

                    foreach (var petId in model.PetId)
                    {
                        if (!model.PetServiceMap.TryGetValue(petId, out var serviceId)) continue;
                        var service = await _db.Services.FindAsync(serviceId);
                        if (service == null || !service.DurationTime.HasValue) continue;

                        string assignedGroomerId = null;

                        if (model.PetGroomerMap?.ContainsKey(petId) == true && !string.IsNullOrEmpty(model.PetGroomerMap[petId]) && model.PetGroomerMap[petId] != "any")
                        {
                            assignedGroomerId = model.PetGroomerMap[petId];
                        }
                        else if (model.PetId.Count == 1 && !string.IsNullOrEmpty(model.StaffId) && model.StaffId != "any")
                        {
                            assignedGroomerId = model.StaffId;
                        }
                        else
                        {
                            if (availableGroomersForAutoAssign == null)
                            {
                                availableGroomersForAutoAssign = await _db.Staffs
                                    .Where(s => s.Position.Contains("Groomer"))
                                    .Include(s => s.Appointments.Where(a => a.AppointmentDateTime.HasValue && a.AppointmentDateTime.Value.Date == model.AppointmentDateTime.Date && a.Status != "Cancelled"))
                                    .ToListAsync();
                            }

                            foreach (var groomer in availableGroomersForAutoAssign)
                            {
                                var appointmentEndTime = model.AppointmentDateTime.AddMinutes(service.DurationTime.Value);
                                bool isBusy = groomer.Appointments.Any(a => model.AppointmentDateTime < a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) && appointmentEndTime > a.AppointmentDateTime);

                                if (!isBusy)
                                {
                                    assignedGroomerId = groomer.UserId;
                                    groomer.Appointments.Add(new Appointment { AppointmentDateTime = model.AppointmentDateTime, DurationTime = service.DurationTime.Value });
                                    break;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(assignedGroomerId))
                        {
                            TempData["ErrorMessage"] = $"Could not find an available groomer for all services at the selected time. Please try another time or assign groomers manually.";
                            return RedirectToAction(nameof(Appointment));
                        }
                        petTasks[petId] = (service, assignedGroomerId);
                    }
                }

                // ========== 4. CREATE APPOINTMENTS ==========

                foreach (var petId in model.PetId)
                {
                    TempData["DebugMessage"] += $"Processing petId={petId}\n";

                    if (!petTasks.TryGetValue(petId, out var task))
                    {
                        TempData["DebugMessage"] += $"Skipping petId={petId}, not in petTasks\n";
                        continue;
                    }

                    var service = task.service;
                    var assignedGroomerId = task.groomerId; // Rename for clarity consistent with pre-processing
                    var duration = service.DurationTime.Value;
                    var appointmentStartTime = appointmentProcessingTime; // Rename for clarity consistent with pre-processing
                    var appointmentEndTime = appointmentStartTime.AddMinutes(duration);

                    // Re-check for conflicts with the *final* assigned groomer and time
                    // (This check should ideally be part of pre-processing for all appointments for better efficiency)
                    bool isBusy = await _db.Appointments.AnyAsync(a =>
                        a.StaffId == assignedGroomerId && a.Status != "Cancelled" &&
                        a.AppointmentDateTime.HasValue &&
                        appointmentStartTime < a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) &&
                        appointmentEndTime > a.AppointmentDateTime.Value);

                    if (isBusy)
                    {
                        var groomer = await _db.Staffs.FindAsync(assignedGroomerId);
                        TempData["ErrorMessage"] = $"Groomer '{groomer?.Name}' has a conflict for the timeslot {appointmentStartTime:t} - {appointmentEndTime:t}. The schedule may have changed. Please try again.";
                        return RedirectToAction(nameof(Appointment));
                    }

                    // Generate a unique AppointmentId
                    while (usedNumbers.Contains(nextIdNum))
                    {
                        nextIdNum++;
                    }
                    var newAppointmentId = "AP" + nextIdNum.ToString("D3");
                    usedNumbers.Add(nextIdNum);
                    nextIdNum++;

                    TempData["DebugMessage"] += $"Generated AppointmentId={newAppointmentId} for petId={petId}\n";

                    // Create the new Appointment object
                    var newAppointment = new Appointment
                    {
                        AppointmentId = newAppointmentId,
                        CustomerId = model.CustomerId,
                        PetId = petId,
                        ServiceId = service.ServiceId,
                        StaffId = assignedGroomerId,
                        AppointmentDateTime = appointmentStartTime,
                        DurationTime = duration,
                        SpecialRequest = model.SpecialRequest ?? "",
                        Status = "Confirmed",
                        AdminId = currentAdminId,
                        CreatedAt = DateTime.Now
                    };

                    newAppointments.Add(newAppointment);
                    TempData["DebugMessage"] += $"Added appointment for petId={petId}, AppointmentId={newAppointment.AppointmentId}\n";

                    // For sequential mode, update the start time for the next appointment
                    if (model.GroomerMode == "one_sequential")
                    {
                        appointmentProcessingTime = appointmentEndTime;
                    }

                    TempData["DebugMessage"] += $"Creating appointment for petId={petId}, AppointmentId={newAppointmentId}\n";
                } // End of outer foreach (var petId in model.PetId)

                // ========== 5. SAVE TO DATABASE ==========

                if (newAppointments.Count == model.PetId.Count)
                {
                    foreach (var appt in newAppointments)
                    {
                        if (string.IsNullOrEmpty(appt.AppointmentId))
                        {
                            TempData["ErrorMessage"] = "Error: Internal error - appointment ID is missing.";
                            return RedirectToAction(nameof(Appointment));
                        }
                    }

                    TempData["DebugMessage"] += $"newAppointments count: {newAppointments.Count}\n";
                    foreach (var appt in newAppointments)
                    {
                        TempData["DebugMessage"] += $"AppointmentId: '{appt.AppointmentId}', CustomerId: '{appt.CustomerId}', PetId: '{appt.PetId}', ServiceId: '{appt.ServiceId}', StaffId: '{appt.StaffId}', AdminId: '{appt.AdminId}'\n";
                        _db.Appointments.AddRange(newAppointments);
                        await _db.SaveChangesAsync();

                        // ========== ADD LOYALTY POINTS FOR EACH APPOINTMENT ==========
                        // Award 10 loyalty points per service to the customer
                        var customer = await _db.Customers.FindAsync(model.CustomerId);
                        if (customer != null)
                        {
                            // 10 points for each appointment created
                            int totalPointsToAdd = newAppointments.Count * 10;
                            customer.LoyaltyPoint += totalPointsToAdd;
                            _db.Customers.Update(customer);
                            await _db.SaveChangesAsync();
                        }

                        TempData["SuccessMessage"] = $"{newAppointments.Count} appointment(s) have been successfully created! Customer earned {newAppointments.Count * 10} loyalty points.";
                    }
                    _db.Appointments.AddRange(newAppointments);
                    await _db.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"{newAppointments.Count} appointment(s) have been successfully created!";
                }
                else // This 'else' belongs to the 'if (newAppointments.Count == model.PetId.Count)'
                    {
                        if (!TempData.ContainsKey("ErrorMessage"))
                        {
                            TempData["ErrorMessage"] = "Could not create all requested appointments. Please check the details and try again.";
                        }
                    }
                }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Appointment Creation Failed: {ex.ToString()}");
                Debug.WriteLine($"[ERROR] Exception Message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[ERROR] Inner Exception: {ex.InnerException.Message}");
                }
                TempData["ErrorMessage"] = "An unexpected error occurred while saving the appointments. Please contact support. Details: " + ex.Message;
            }

            return RedirectToAction(nameof(Appointment));
        }
        else if (actionType == "edit")
        {
            if (string.IsNullOrEmpty(model.EditAppointmentId))
            {
                TempData["ErrorMessage"] = "Appointment ID was missing. Cannot update.";
                return RedirectToAction(nameof(Appointment));
            }

            var appointmentToUpdate = await _db.Appointments.FindAsync(model.EditAppointmentId);

            if (appointmentToUpdate == null)
            {
                TempData["ErrorMessage"] = "Appointment not found. It may have been deleted.";
                return RedirectToAction(nameof(Appointment));
            }

            if (appointmentToUpdate.Status != "Confirmed" || model.Status != "Completed")
            {
                TempData["ErrorMessage"] = "Invalid status change. Only 'Confirmed' appointments can be changed to 'Completed'.";

                // 重定向回当前筛选结果，并保持编辑状态
                return RedirectToAction(nameof(Appointment), new { status = model.FilterStatus, groomerid = model.FilterGroomerId, date = model.FilterDate?.ToString("yyyy-MM-dd"), editId = model.EditAppointmentId });
            }

            if (appointmentToUpdate.AppointmentDateTime.HasValue && appointmentToUpdate.DurationTime.HasValue && appointmentToUpdate.DurationTime.Value > 0)
            {
                var appointmentEnd = appointmentToUpdate.AppointmentDateTime.Value.AddMinutes(appointmentToUpdate.DurationTime.Value);
                if (DateTime.Now < appointmentEnd)
                {
                    TempData["ErrorMessage"] = $"Cannot complete appointment yet. The service finishes at {appointmentEnd:MMM dd, hh:mm tt}.";
                    return RedirectToAction(nameof(Appointment), new { status = model.FilterStatus, groomerid = model.FilterGroomerId, date = model.FilterDate?.ToString("yyyy-MM-dd"), editId = model.EditAppointmentId });
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Cannot complete appointment because appointment time or duration is missing.";
                return RedirectToAction(nameof(Appointment), new { status = model.FilterStatus, groomerid = model.FilterGroomerId, date = model.FilterDate?.ToString("yyyy-MM-dd"), editId = model.EditAppointmentId });
            }

            appointmentToUpdate.Status = model.Status;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Appointment {appointmentToUpdate.AppointmentId} has been successfully updated!";

            return RedirectToAction(nameof(Appointment));
        }

        // 默认返回 - 所有代码路径现在都有返回值
        return RedirectToAction(nameof(Appointment));
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
            // ========== VALIDATE INPUTS USING VALIDATION SERVICE =========
            var validationErrors = await ValidateStaffAsync(staff);

            if (validationErrors.Any())
            {
                return Json(new { success = false, errors = validationErrors });
            }

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

                bool isBusy = await _db.Appointments.AnyAsync(a =>
                    a.StaffId == groomerId &&
                    a.AppointmentDateTime < finalEndTime &&
                    a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) > appointmentStartTime);

                if (isBusy)
                {
                    var groomer = await _db.Staffs.FindAsync(groomerId);
                    return Json(new { isValid = false, field = "StaffId", message = $"Groomer '{groomer?.Name}' is unavailable at this time." });
                }
            }
        }
        // --- "Any Groomer" validation is too complex for real-time and is best handled on final submission ---

        return Json(new { isValid = true });
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

        var gifts = await _db.RedeemGifts
            .Where(g => g.IsDeleted == false)   // filter out deleted items
            .OrderByDescending(g => g.GiftId)
            .ToListAsync();

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
                ViewData["Error"] = "? Unable to save: Admin is not logged in. Please login again.";
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
            if (string.IsNullOrEmpty(deleteGiftId))
                return NotFound();

            var dbGift = await _db.RedeemGifts
                .IgnoreQueryFilters() // important if you added global filter
                .FirstOrDefaultAsync(g => g.GiftId == deleteGiftId);

            if (dbGift == null)
                return NotFound();

            dbGift.IsDeleted = true; // ✅ SOFT DELETE
            _db.RedeemGifts.Update(dbGift);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Gift deleted successfully!";
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
            query = query.Where(s => s.ServiceServiceCategories.Any(ssc => ssc.Category.PetType.Contains(Category)));

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
            var allServiceIds = await _db.Services
                .Select(s => s.ServiceId)
                .ToListAsync();

            string newServiceId;

            if (!allServiceIds.Any())
            {
                newServiceId = "SE001";
            }
            else
            {
                var usedNumbers = allServiceIds
                    .Select(id => int.Parse(id.Substring(2)))
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
                
                newServiceId = "SE" + nextNumber.ToString("D3");
            }

            service.ServiceId = newServiceId;
            service.AdminId = adminId;
            service.Status = service.Status ?? "Active";
            _db.Services.Add(service);

            // ===== CREATE SSC =====
            var selectedCats = SelectedCategories?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList() ?? new List<string>();
            TempData["Debug_RawSelectedCategories_Create"] = SelectedCategories != null && SelectedCategories.Any() ? string.Join(", ", SelectedCategories) : "None (raw)";
            TempData["Debug_FilteredSelectedCats_Create"] = selectedCats.Any() ? string.Join(", ", selectedCats) : "None (filtered)";

            if (!selectedCats.Any())
            {
                // No categories selected, just save the service
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Service));
            }


            var sscEntries = new List<ServiceServiceCategory>();
            var assignedSscIds = new HashSet<string>();
            foreach (var catId in selectedCats)
            {
                int nextNum = await GetNextAvailableNumericId("SSC", assignedSscIds);
                string newSscId = $"SSC{nextNum:D3}";
                assignedSscIds.Add(newSscId); // Add to the set of assigned IDs for this batch

                var ssc = new ServiceServiceCategory
                {
                    SscId = newSscId,
                    ServiceId = service.ServiceId,
                    CategoryId = catId
                };
                TempData[$"Debug_SSC_Create_{catId}"] = $"catId: {catId}, newSscId: {newSscId}";
                sscEntries.Add(ssc);
            }

            _db.ServiceServiceCategories.AddRange(sscEntries);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Service '{service.Name}' created successfully!";

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
            var editSelectedCats = SelectedCategories?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList() ?? new List<string>();

            if (editSelectedCats.Any())
            {
                var sscEntries = new List<ServiceServiceCategory>();
                var assignedSscIds = new HashSet<string>();
                foreach (var catId in editSelectedCats)
                {
                    int nextNum = await GetNextAvailableNumericId("SSC", assignedSscIds);
                    string newSscId = $"SSC{nextNum:D3}";
                    assignedSscIds.Add(newSscId); // Add to the set of assigned IDs for this batch

                    var ssc = new ServiceServiceCategory
                    {
                        SscId = newSscId,
                        ServiceId = dbService.ServiceId,
                        CategoryId = catId
                    };
                    TempData[$"Debug_SSC_Edit_{catId}"] = $"catId: {catId}, newSscId: {newSscId}"; sscEntries.Add(ssc);
                }

                _db.ServiceServiceCategories.AddRange(sscEntries);
            }

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Service '{service.Name}' updated successfully!";
            return RedirectToAction(nameof(Service));
        }

        // ================= DELETE =================
        else if (actionType == "delete")
        {
            if (string.IsNullOrEmpty(deleteServiceId))
                return NotFound();

            var dbService = await _db.Services
 .Include(s => s.ServiceServiceCategories)
 .FirstOrDefaultAsync(s => s.ServiceId == deleteServiceId);
            if (dbService == null)
                return NotFound();

            // Remove related junction rows first to avoid FK constraint errors
            if (dbService.ServiceServiceCategories != null && dbService.ServiceServiceCategories.Any())
            {
                _db.ServiceServiceCategories.RemoveRange(dbService.ServiceServiceCategories);
            }

            _db.Services.Remove(dbService);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Service deleted successfully!";
            return RedirectToAction(nameof(Service));
        }

        return RedirectToAction(nameof(Service));
    }

    // ========== PET ==========
    public IActionResult Pet()
    {
        ViewData["ActivePage"] = "Pet";
        return View();
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAppointmentAjax([FromForm] string appointmentId)
    {
        try
        {
            if (string.IsNullOrEmpty(appointmentId))
            {
                return Json(new { success = false, message = "Invalid appointment id." });
            }

            var dbAppointment = await _db.Appointments.FindAsync(appointmentId);

            if (dbAppointment == null)
            {
                return Json(new { success = false, message = "Appointment not found." });
            }

            _db.Appointments.Remove(dbAppointment);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Appointment deleted successfully." });
        }
        catch (Exception)
        {
            // Log the error in a real app
            return Json(new { success = false, message = "Failed to delete appointment." });
        }
    }


      [HttpGet]
    public async Task<IActionResult> SearchCustomers(string term)
    {
        // When term is empty, return a default set of customers so opening the Select2 shows DB entries.
        List<object> customers;

        if (string.IsNullOrWhiteSpace(term))
        {
            customers = await _db.Customers
                .Where(c => c.Status == "Active")
                .OrderBy(c => c.Name)
                .Select(c => new { id = c.UserId, text = c.Name + " (" + c.Phone + ")" })
                .Take(20)
                .ToListAsync<object>();
        }
        else
        {
            customers = await _db.Customers
                .Where(c => c.Name.Contains(term) || c.Phone.Contains(term) || c.Email.Contains(term))
                .OrderBy(c => c.Name)
                .Select(c => new { id = c.UserId, text = c.Name + " (" + c.Phone + ")" })
                .Take(50)
                .ToListAsync<object>();
        }

        // Return Select2-friendly shape
        return Json(new { results = customers });
    }
    // ========== REPORTS ==========

    public IActionResult reports()
    {
        ViewData["ActivePage"] = "reports";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetBookingDensity(int month, int year)
    {
        try
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1);

            var bookingCounts = await _db.Appointments
                .Where(a => a.AppointmentDateTime >= startDate && a.AppointmentDateTime < endDate && a.Status != "Cancelled")
                .GroupBy(a => a.AppointmentDateTime.Value.Date)
                .Select(g => new {
                    Date = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var countsDict = bookingCounts.ToDictionary(
                item => item.Date.ToString("yyyy-MM-dd"),
                item => item.Count
            );

            return Json(new { success = true, counts = countsDict });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "Could not retrieve booking data." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GenerateReportData(DateTime startDate, DateTime endDate, string reportType)
    {
        try
        {
            // Adjust endDate to include the whole day
            var adjustedEndDate = endDate.AddDays(1);

            var appointments = await _db.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Staff)
                .Include(a => a.Service)
                .Include(a => a.Pet)
                .Where(a => a.AppointmentDateTime >= startDate && a.AppointmentDateTime < adjustedEndDate)
                .ToListAsync();

            var totalAppointments = appointments.Count;
            object reportData;

            switch (reportType)
            {
                case "volume":
                    reportData = new
                    {
                        TotalAppointments = totalAppointments,
                        StartDate = startDate.ToString("MMM dd, yyyy"),
                        EndDate = endDate.ToString("MMM dd, yyyy"),
                        GroupedData = new Dictionary<string, object>
                        {
                            {
                                "Appointments by Groomer", appointments
                                    .Where(a => a.Staff != null)
                                    .GroupBy(a => a.Staff.Name)
                                    .Select(g => new { Label = g.Key, Value = g.Count() })
                                    .OrderByDescending(x => x.Value)
                                    .ToList<object>()
                            },
                            {
                                "Appointments by Service", appointments
                                    .Where(a => a.Service != null)
                                    .GroupBy(a => a.Service.Name)
                                    .Select(g => new { Label = g.Key, Value = g.Count() })
                                    .OrderByDescending(x => x.Value)
                                    .ToList<object>()
                            },
                            {
                                "Appointments by Pet Type", appointments
                                    .Where(a => a.Pet != null)
                                    .GroupBy(a => a.Pet.Type)
                                    .Select(g => new { Label = g.Key, Value = g.Count() })
                                    .OrderByDescending(x => x.Value)
                                    .ToList<object>()
                            }
                        }
                    };
                    break;

                case "workload":
                    reportData = new
                    {
                        TotalAppointments = totalAppointments,
                        StartDate = startDate.ToString("MMM dd, yyyy"),
                        EndDate = endDate.ToString("MMM dd, yyyy"),
                        GroupedData = new Dictionary<string, object>
                        {
                            {
                                "Groomer Workload", appointments
                                    .Where(a => a.Staff != null)
                                    .GroupBy(a => a.Staff.Name)
                                    .Select(g => new
                                    {
                                        Label = g.Key,
                                        Value = $"{g.Count()} appointments ({g.Sum(a => a.DurationTime ?? 0)} mins)"
                                    })
                                    .OrderByDescending(x => x.Label)
                                    .ToList<object>()
                            }
                        }
                    };
                    break;

                case "topCustomers":
                    reportData = new
                    {
                        TotalAppointments = totalAppointments,
                        StartDate = startDate.ToString("MMM dd, yyyy"),
                        EndDate = endDate.ToString("MMM dd, yyyy"),
                        GroupedData = new Dictionary<string, object>
                        {
                            {
                                "Top Customers by Appointments", appointments
                                    .Where(a => a.Customer != null)
                                    .GroupBy(a => a.Customer.Name)
                                    .Select(g => new { Label = g.Key, Value = g.Count() })
                                    .OrderByDescending(x => x.Value)
                                    .Take(20) // Limit to top 20 customers
                                    .ToList<object>()
                            }
                        }
                    };
                    break;

                default:
                    return Json(new { success = false, message = "Invalid report type." });
            }

            return Json(new { success = true, data = reportData });
        }
        catch (Exception ex)
        {
            // Log the exception details on the server
            Console.WriteLine($"Error generating report: {ex.Message}");
            return Json(new { success = false, message = "An error occurred while generating the report." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> DownloadReportAsExcel(DateTime startDate, DateTime endDate, string reportType)
    {
        try
        {
            var adjustedEndDate = endDate.AddDays(1);

            var appointments = await _db.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Staff)
                .Include(a => a.Service)
                .Include(a => a.Pet)
                .Where(a => a.AppointmentDateTime >= startDate && a.AppointmentDateTime < adjustedEndDate)
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Report Type: {reportType}");
            sb.AppendLine($"Date Range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            sb.AppendLine(); // Add a blank line

            switch (reportType)
            {
                case "volume":
                    sb.AppendLine("Appointments by Groomer");
                    sb.AppendLine("Groomer,Appointment Count");
                    var byGroomer = appointments.Where(a => a.Staff != null).GroupBy(a => a.Staff.Name)
                                                .Select(g => new { Label = g.Key, Value = g.Count() });
                    foreach (var item in byGroomer) sb.AppendLine($"{item.Label},{item.Value}");
                    sb.AppendLine();

                    sb.AppendLine("Appointments by Service");
                    sb.AppendLine("Service,Appointment Count");
                    var byService = appointments.Where(a => a.Service != null).GroupBy(a => a.Service.Name)
                                                .Select(g => new { Label = g.Key, Value = g.Count() });
                    foreach (var item in byService) sb.AppendLine($"{item.Label},{item.Value}");
                    sb.AppendLine();

                    sb.AppendLine("Appointments by Pet Type");
                    sb.AppendLine("Pet Type,Appointment Count");
                    var byPetType = appointments.Where(a => a.Pet != null).GroupBy(a => a.Pet.Type)
                                                .Select(g => new { Label = g.Key, Value = g.Count() });
                    foreach (var item in byPetType) sb.AppendLine($"{item.Label},{item.Value}");
                    break;

                case "workload":
                    sb.AppendLine("Groomer,Appointment Count,Total Duration (mins)");
                    var workload = appointments.Where(a => a.Staff != null).GroupBy(a => a.Staff.Name)
                                               .Select(g => new
                                               {
                                                   Label = g.Key,
                                                   Count = g.Count(),
                                                   Duration = g.Sum(a => a.DurationTime ?? 0)
                                               });
                    foreach (var item in workload) sb.AppendLine($"{item.Label},{item.Count},{item.Duration}");
                    break;

                case "topCustomers":
                    sb.AppendLine("Customer Name,Appointment Count");
                    var topCustomers = appointments.Where(a => a.Customer != null).GroupBy(a => a.Customer.Name)
                                                   .Select(g => new { Label = g.Key, Value = g.Count() })
                                                   .OrderByDescending(x => x.Value).Take(100);
                    foreach (var item in topCustomers) sb.AppendLine($"{item.Label},{item.Value}");
                    break;

                default:
                    sb.AppendLine("Invalid report type specified.");
                    break;
            }

            var fileName = $"Report_{reportType}_{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv";
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Error downloading report as Excel: {ex.Message}");
            // Return a simple text file with the error message
            return Content("An error occurred while generating the Excel file: " + ex.Message);
        }
    }

    [HttpGet]
    public JsonResult GetServicesByPetType(string petType)
    {
    if (string.IsNullOrEmpty(petType))
    {
        return Json(new List<object>());
    }

    var services = _db.Services
        .Where(s => s.Status == "Active" && s.ServiceServiceCategories.Any(ssc => ssc.Category.PetType == petType))
        .Select(s => new { id = s.ServiceId, text = s.Name })
        .ToList();

    return Json(services);
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
            var errors = await ValidateStaffAsync(staff);

            // If there are validation errors, return them
            if (errors.Any())
            {
                return Json(new { success = false, errors = errors });
            }

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
                TempData["ErrorMessage"] = "Admin not logged in. Please login again.";
                return RedirectToAction("Login", "Auth");
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
                    staff.Email,
                    staff.Name,
                    newStaffId,
                    temporaryPassword,
                    staff.Email,
                    staff.Phone,
                    loginUrl
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
            var validationErrors = await ValidateStaffAsync(staff, editStaffId);

            // If there are validation errors, return them
            if (validationErrors.Any())
            {
                return Json(new { success = false, errors = validationErrors });
            }

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
                            // Log error but don't fail the operation
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
    public async Task<JsonResult> DeleteServiceAjax([FromForm] string serviceId)
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

            // Remove related junction rows first to avoid FK constraint errors
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
            // Return a JSON error so all code paths return a value
            return Json(new { success = false, message = "Failed to delete service." });
        }
    }

    private async Task<int> GetNextAvailableNumericId(string prefix, HashSet<string> assignedIdsInCurrentBatch)

    {

        var allExistingIds = await _db.ServiceServiceCategories

            .Where(x => x.SscId != null && x.SscId.StartsWith(prefix))

            .Select(x => x.SscId)

            .ToListAsync();



        // Combine existing database IDs with IDs assigned in the current batch

        var allUsedIds = allExistingIds.Concat(assignedIdsInCurrentBatch)

                                       .Where(id => id.StartsWith(prefix))

                                       .ToList();



        var usedNumbers = allUsedIds

            .Select(id =>

            {

                if (id.Length <= prefix.Length) return -1;

                return int.TryParse(id.Substring(prefix.Length), out var n) ? n : -1;

            })

            .Where(n => n > 0)

            .OrderBy(n => n)

            .ToList();



        int nextNumber = 1;

        foreach (var num in usedNumbers)

        {

            if (num != nextNumber)

            {

                break; // Found a gap

            }

            nextNumber++;

        }

        return nextNumber;

    }

    // ========== APPOINTMENT AJAX ENDPOINTS ==========

    // GET: Search customers for Select2 AJAX
    [HttpGet]
    public async Task<JsonResult> SearchCustomers(string term = "")
    {
        try
        {
            var query = _db.Customers.AsQueryable();

            if (!string.IsNullOrEmpty(term))
            {
                term = term.ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(term) ||
                    c.Email.ToLower().Contains(term) ||
                    c.Phone.Contains(term));
            }

            var results = await query
                .Take(20) // Limit to 20 results
                .Select(c => new
                {
                    id = c.UserId,
                    text = c.Name + " (" + c.Phone + ")"
                })
                .ToListAsync();

            return Json(new { results = results });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SearchCustomers: {ex.Message}");
            return Json(new { results = new List<object>() });
        }
    }

    // GET: Get pets by customer ID
    [HttpGet]
    public async Task<JsonResult> GetPetsByCustomerId(string customerId)
    {
        try
        {
            if (string.IsNullOrEmpty(customerId))
                return Json(new { results = new List<object>() });

            var pets = await _db.Pets
                .Where(p => p.CustomerId == customerId)
                .Select(p => new
                {
                    id = p.PetId,
                    text = p.Name,
                    type = p.Type
                })
                .ToListAsync();

            return Json(new { results = pets });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetPetsByCustomerId: {ex.Message}");
            return Json(new { results = new List<object>() });
        }
    }

    // GET: Get services by pet type
    [HttpGet]
    public async Task<JsonResult> GetServicesByPetType(string petType)
    {
        try
        {
            if (string.IsNullOrEmpty(petType))
                return Json(new List<object>());

            var services = await _db.Services
    .Include(s => s.ServiceServiceCategories)
    .ThenInclude(ssc => ssc.Category)
    .Where(s => s.Status == "Active" && s.ServiceServiceCategories.Any(ssc => ssc.Category.PetType == petType))
    .Select(s => new
    {
        id = s.ServiceId,
        text = s.Name,
        duration = s.DurationTime
    })
    .ToListAsync();

            return Json(services);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetServicesByPetType: {ex.Message}");
            return Json(new List<object>());
        }
    }

    // GET: Get booking density for calendar visualization
    [HttpGet]
    public async Task<JsonResult> GetBookingDensity(int year, int month)
    {
        try
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Get total available minutes per day (assuming 8 hours = 480 minutes per day)
            int totalMinutesPerDay = 480;

            // Get appointments and their durations
            var appointmentsByDay = await _db.Appointments
                .Where(a => a.AppointmentDateTime >= startDate &&
                            a.AppointmentDateTime <= endDate &&
                            a.Status != "Cancelled")
                .GroupBy(a => a.AppointmentDateTime.Value.Date)
                .Select(g => new
                {
                    date = g.Key,
                    bookedMinutes = g.Sum(a => a.DurationTime ?? 0)
                })
                .ToListAsync();

            // Convert to dictionary format
            var counts = appointmentsByDay.ToDictionary(
                x => x.date.ToString("yyyy-MM-dd"),
                x => new { bookedMinutes = x.bookedMinutes }
            );

            return Json(new
            {
                success = true,
                totalMinutesPerDay = totalMinutesPerDay,
                counts = counts
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetBookingDensity: {ex.Message}");
            return Json(new { success = false, message = "Could not retrieve booking density." });
        }
    }

    // POST: Delete appointment (AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<JsonResult> DeleteAppointmentAjax(string appointmentId)
    {
        try
        {
            if (string.IsNullOrEmpty(appointmentId))
                return Json(new { success = false, message = "Invalid appointment ID." });

            var appointment = await _db.Appointments.FindAsync(appointmentId);
            if (appointment == null)
                return Json(new { success = false, message = "Appointment not found." });

            _db.Appointments.Remove(appointment);
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Appointment deleted successfully." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DeleteAppointmentAjax: {ex.Message}");
            return Json(new { success = false, message = "Failed to delete appointment." });
        }
    }

    // POST: Validate appointment time
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<JsonResult> ValidateAppointmentTime([FromBody] AppointmentValidationRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrEmpty(request.AppointmentDateTime) ||
                string.IsNullOrEmpty(request.StaffId))
            {
                return Json(new { isValid = false, field = "StaffId", message = "Missing required fields." });
            }

            // Parse the appointment date/time
            if (!DateTime.TryParse(request.AppointmentDateTime, out DateTime appointmentTime))
            {
                return Json(new { isValid = false, field = "AppointmentDateTime", message = "Invalid date/time format." });
            }

            // Validate groomer availability
            if (request.StaffId != "any")
            {
                var existingAppointments = await _db.Appointments
                    .Where(a => a.StaffId == request.StaffId &&
                                a.AppointmentDateTime.Value.Date == appointmentTime.Date &&
                                a.Status != "Cancelled")
                    .ToListAsync();

                // Check if any existing appointment conflicts with the requested time
                // (This is a simple check - you may need more sophisticated logic)
                if (existingAppointments.Any())
                {
                    return Json(new { isValid = false, field = "StaffId", message = "This groomer is not available at this time." });
                }
            }

            return Json(new { isValid = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ValidateAppointmentTime: {ex.Message}");
            return Json(new { isValid = false, field = "StaffId", message = "Validation error occurred." });
        }
    }

    // Request class for AJAX validation
    public class AppointmentValidationRequest
    {
        public string? AppointmentDateTime { get; set; }
        public string? StaffId { get; set; }
        public string? GroomerMode { get; set; }
        public Dictionary<string, string>? PetServiceMap { get; set; }
        public Dictionary<string, string>? PetGroomerMap { get; set; }
    }

    // Helper: Validate staff object on server-side for create/edit flows
    private async Task<Dictionary<string, string>> ValidateStaffAsync(Models.Staff staff, string? existingStaffId = null)
    {
        var errors = new Dictionary<string, string>();
        if (staff == null)
        {
            errors["General"] = "Staff data is required.";
            return errors;
        }

        var name = staff.Name ?? string.Empty;
        var ic = staff.IC ?? string.Empty;
        var email = staff.Email ?? string.Empty;
        var phone = staff.Phone ?? string.Empty;
        var desc = staff.Description ?? string.Empty;

        if (!_validationService.ValidateName(name))
            errors["Name"] = "Name must be 3-200 characters and contain only letters/spaces.";

        if (!_validationService.ValidateICFormat(ic))
            errors["IC"] = "IC format must be xxxxxx-xx-xxxx and age 18-60.";

        if (!_validationService.ValidateEmail(email))
            errors["Email"] = "Please enter a valid email address.";

        var formattedPhone = _phoneService.FormatPhoneNumber(phone);
        if (!_phoneService.ValidatePhoneFormat(formattedPhone))
            errors["Phone"] = "Phone format must be 01X-XXXXXXX or 01X-XXXXXXXX.";

        if (staff.ExperienceYear.HasValue && !_validationService.ValidateExperienceYear(staff.ExperienceYear.Value))
        {
            errors["ExperienceYear"] = "Experience must be between 0 and 50 years.";
        }

        // Cross-validate experience vs IC if both present
        if (!string.IsNullOrEmpty(ic) && staff.ExperienceYear.HasValue)
        {
            var vr = _validationService.ValidateExperienceAgainstAge(staff.ExperienceYear.Value, ic);
            if (!vr.IsValid) errors["ExperienceYear"] = vr.ErrorMessage;
        }

        if (!string.IsNullOrEmpty(desc) && desc.Length > 500)
            errors["Description"] = "Description must be 500 characters or fewer.";

        return errors;
    }
}
