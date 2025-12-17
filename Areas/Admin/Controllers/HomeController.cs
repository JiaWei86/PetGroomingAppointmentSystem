using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Models.ViewModels;
using PetGroomingAppointmentSystem.Areas.Admin.Controllers;
using PetGroomingAppointmentSystem.Areas.Admin.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering; // Add this using directive

namespace PetGroomingAppointmentSystem.Areas.Admin.Controllers;

[Area("Admin")]
[AdminOnly]
public class HomeController : Controller
{
    private readonly DB _context;
    private readonly IEmailService _emailService;
    private readonly IPasswordService _passwordService;
    private readonly IPhoneService _phoneService;
    private readonly IValidationService _validationService;

    public HomeController(
    DB context,
    IEmailService emailService,
    IPasswordService passwordService,
    IPhoneService phoneService,
    IValidationService validationService)
    {
        _context = context;
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

 int currentMonthAppointments = await _context.Appointments
 .CountAsync(a => a.AppointmentDateTime >= firstDayOfCurrentMonth && a.AppointmentDateTime < firstDayOfCurrentMonth.AddMonths(1));

 int lastMonthAppointments = await _context.Appointments
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
 viewModel.ActiveGroomers.Count = await _context.Staffs.CountAsync(s => s.Role == "staff");

 // 总顾客数量 (替换了原来的"待处理预约")
 viewModel.PendingAppointments.Count = await _context.Customers.CountAsync();


 // --- 2. 填充忠诚度积分 (Loyalty Points) ---
 // 注意: 以下是基于现有模型的模拟。您需要根据您的业务逻辑调整。
 var startOfWeek = now.AddDays(-(int)now.DayOfWeek);
 var endOfWeek = startOfWeek.AddDays(7);

 // 假设每次完成的预约都奖励10个积分
 viewModel.LoyaltyPoints.AwardedThisWeek = await _context.Appointments
 .CountAsync(a => a.Status == "Completed" && a.AppointmentDateTime >= startOfWeek && a.AppointmentDateTime < endOfWeek) * 10;

 // 活跃会员数 (假设为总顾客数)
 viewModel.LoyaltyPoints.ActiveMembers = await _context.Customers.CountAsync();

 // 假设每次使用礼品兑换都消耗500积分
 viewModel.LoyaltyPoints.RedeemedThisWeek = 0; // 您需要一个礼品兑换记录表来计算这个值


 // --- 3. 填充图表数据 (Chart Data) ---

 // 周视图 (过去7天)
 var last7Days = Enumerable.Range(0, 7).Select(i => now.AddDays(-i).Date).Reverse().ToList();
 var weeklyData = await _context.Appointments
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
 int weekCount = await _context.Appointments
 .CountAsync(a => a.AppointmentDateTime >= weekStart && a.AppointmentDateTime < weekEnd);
 monthData.Add(weekCount);
 }
 viewModel.ChartData.Month = new ChartSeriesModel { Labels = weeksInMonth, Data = monthData };

 // 日视图 (今天按小时)
 var todayStart = now.Date.AddHours(9); // 9 AM
 var todayEnd = now.Date.AddHours(17); // 5 PM
 var hourlyData = await _context.Appointments
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

 viewModel.AppointmentsForCalendar = await _context.Appointments
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
                    // If StaffId is provided (edit mode), exclude it from the check.
                    isDuplicate = string.IsNullOrEmpty(request.StaffId)
                        ? await _context.Staffs.AnyAsync(s => s.IC == valueToCheck)
                        : await _context.Staffs.AnyAsync(s => s.IC == valueToCheck && s.UserId != request.StaffId);

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
                        ? await _context.Staffs.AnyAsync(s => s.Email.ToLower() == valueToCheck.ToLower())
                        : await _context.Staffs.AnyAsync(s => s.Email.ToLower() == valueToCheck.ToLower() && s.UserId != request.StaffId);

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
                        ? await _context.Staffs.AnyAsync(s => s.Phone == formattedPhone)
                        : await _context.Staffs.AnyAsync(s => s.Phone == formattedPhone && s.UserId != request.StaffId);

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
        var groomers = await _context.Staffs
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
            var allStaffIds = await _context.Staffs
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
                TempData["ErrorMessage"] = "? Admin not logged in. Please login again.";
                return RedirectToAction(nameof(Groomer));
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
            _context.Staffs.Add(staff);
            await _context.SaveChangesAsync();

            // ========== SEND EMAIL WITH CREDENTIALS USING EMAIL SERVICE ==========
            try
            {
                var loginUrl = $"{Request.Scheme}://{Request.Host}/Admin/Auth/Login";

                // Defensive check: if another staff (different id) already has this email, skip sending credentials.
                bool duplicateAfterSave = await _context.Staffs.AnyAsync(s => s.Email == staff.Email && s.UserId != newStaffId);
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

            var dbStaff = await _context.Staffs.FindAsync(editStaffId);
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

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $" Groomer '{staff.Name}' updated successfully!";
            return RedirectToAction(nameof(Groomer));
        }

        // --- DELETE ---
        else if (actionType == "delete")
        {
            if (string.IsNullOrEmpty(deleteStaffId)) return NotFound();

            var dbStaff = await _context.Staffs.FindAsync(deleteStaffId);
            if (dbStaff == null) return NotFound();

            _context.Staffs.Remove(dbStaff);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = " Staff deleted successfully!";

            return RedirectToAction(nameof(Groomer));
        }

        // Fallback
        var allGroomers = await _context.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
        return View(allGroomers);
    }

    // ========== CUSTOMER ==========
    public IActionResult Customer()
    {
        ViewData["ActivePage"] = "Customer";
        return View();
    }

    // ========== APPOINTMENT ==========
    public async Task<IActionResult> Appointment(string status, string groomerid, string date, string editId)
 {
 ViewData["ActivePage"] = "Appointment";
 // Base query
 var query = _context.Appointments
 .Include(a => a.Customer)
 .Include(a => a.Pet)
 .Include(a => a.Staff)
 .Include(a => a.Service)
 .AsQueryable();

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

        // Load lookup data for the ViewModel
        var staffList = await _context.Staffs
            .Select(s => new SelectListItem { Value = s.UserId, Text = s.Name })
            .ToListAsync();

        var customerList = await _context.Customers
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.UserId, Text = c.Name + " (" + c.Phone + ")" })
            .ToListAsync();

        // Create and populate the ViewModel
        var viewModel = new AppointmentViewModel
        {
            Appointments = appointments,
            StaffList = staffList,
            CustomerList = customerList,
            FilterStatus = status,
            FilterGroomerId = groomerid,
            FilterDate = DateTime.TryParse(date, out var parsedFilterDate) ? parsedFilterDate : (DateTime?)null
        };
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
                // 将所有模型验证错误合并成一条消息
                var errors = ModelState.Values
                                    .SelectMany(v => v.Errors)
                                    .Select(e => e.ErrorMessage)
                                    .ToList();
                 TempData["ErrorMessage"] = "Data is invalid. Please check your inputs. Details: " + string.Join("; ", errors);
                return RedirectToAction(nameof(Appointment));
            }
    
            // 2. 检查宠物和服务的数量是否匹配 (使用 PetServiceMap)
            if (model.PetId == null || model.PetServiceMap == null || model.PetId.Count == 0 || model.PetId.Count != model.PetServiceMap.Count)
            {
                TempData["ErrorMessage"] = "An error occurred. The number of selected pets must match the number of selected services. Please try again.";
                return RedirectToAction(nameof(Appointment));
            }
    
            try
            {
                var newAppointments = new List<Appointment>();
                var appointmentStartTime = model.AppointmentDateTime;


                // 如果是单一美容师，先检查总时长是否会超过下班时间 (4:30 PM)
                if (model.StaffId != "any" && !string.IsNullOrEmpty(model.StaffId) && model.PetId.Count > 1)
                {
                    double totalDuration = 0;
                    foreach (var petId in model.PetId)
                    {
                        if (model.PetServiceMap.TryGetValue(petId, out var serviceId))
                        {
                            var service = await _context.Services.FindAsync(serviceId);
                            if (service?.DurationTime.HasValue ?? false)
                            {
                                totalDuration += service.DurationTime.Value;
                            }
                        }
                    }
                    var finalEndTime = appointmentStartTime.AddMinutes(totalDuration);
                    if (finalEndTime.TimeOfDay > new TimeSpan(16, 30, 0))
                    {
                        TempData["ErrorMessage"] = $"The total duration for all services exceeds the closing time of 4:30 PM. The estimated finish time is {finalEndTime:hh:mm tt}.";
                        return RedirectToAction(nameof(Appointment));
                    }
                }

                // 如果是为每只宠物单独分配美容师
                if (model.GroomerMode == "individual" && model.PetGroomerMap != null)
                {
                    // 检查是否有任何一只宠物没有分配美容师
                    if (model.PetId.Any(petId => !model.PetGroomerMap.ContainsKey(petId) || string.IsNullOrEmpty(model.PetGroomerMap[petId])))
                    {
                        TempData["ErrorMessage"] = "When assigning individually, every pet must be assigned a groomer.";
                        return RedirectToAction(nameof(Appointment));
                    }

                    // 按美容师分组，检查每个美容师的时间安排
                    var groomerTasks = model.PetGroomerMap.GroupBy(kv => kv.Value);
                    foreach (var group in groomerTasks)
                    {
                        var groomerId = group.Key;
                        double totalDurationForGroomer = 0;
                        foreach (var petGroomerPair in group)
                        {
                            if (model.PetServiceMap.TryGetValue(petGroomerPair.Key, out var serviceId))
                            {
                                var service = await _context.Services.FindAsync(serviceId);
                                if (service?.DurationTime.HasValue ?? false)
                                {
                                    totalDurationForGroomer += service.DurationTime.Value;
                                }
                            }
                        }
                        var finalEndTime = appointmentStartTime.AddMinutes(totalDurationForGroomer);
                        if (finalEndTime.TimeOfDay > new TimeSpan(16, 30, 0))
                        {
                            var groomer = await _context.Staffs.FindAsync(groomerId);
                            TempData["ErrorMessage"] = $"The total duration for services assigned to {groomer?.Name} exceeds the closing time of 4:30 PM.";
                            return RedirectToAction(nameof(Appointment));
                        }
                    }
                }
                // 优化ID生成：一次性获取所有ID
                var allAppointmentIds = await _context.Appointments.Select(a => a.AppointmentId).ToListAsync();
                var usedNumbers = allAppointmentIds
                    .Where(id => !string.IsNullOrEmpty(id) && id.StartsWith("AP"))
                    .Select(id => int.TryParse(id.Substring(2), out var n) ? n : -1)
                    .Where(n => n > 0)
                    .ToHashSet();
                int nextIdNum = 1;

                // 如果用户指定了美容师
                // 如果是为每只宠物单独分配美容师
                if (model.GroomerMode == "individual")
                {
                    foreach (var petId in model.PetId)
                    {
                        if (!model.PetServiceMap.TryGetValue(petId, out var serviceId) || !model.PetGroomerMap.TryGetValue(petId, out var groomerId)) continue;

                        var service = await _context.Services.FindAsync(serviceId);
                        if (service == null || !service.DurationTime.HasValue) continue;

                        var appointmentEndTime = appointmentStartTime.AddMinutes(service.DurationTime.Value);

                        bool isBusy = await _context.Appointments.AnyAsync(a =>
                            a.StaffId == groomerId &&
                            a.AppointmentDateTime < appointmentEndTime &&
                            a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) > appointmentStartTime);

                        if (isBusy)
                        {
                            var groomer = await _context.Staffs.FindAsync(groomerId);
                            TempData["ErrorMessage"] = $"Groomer '{groomer?.Name}' is not available at the selected time for one of the pets. Please check availability.";
                            return RedirectToAction(nameof(Appointment));
                        }

                        while (usedNumbers.Contains(nextIdNum)) nextIdNum++;
                        var newAppointmentId = "AP" + nextIdNum.ToString("D3");
                        usedNumbers.Add(nextIdNum);

                        string currentAdminId = HttpContext.Session.GetString("AdminId");
                        if (string.IsNullOrEmpty(currentAdminId))
                        {
                            TempData["ErrorMessage"] = "Your session has expired. Please log in again.";
                            return RedirectToAction(nameof(Appointment));
                        }

                        newAppointments.Add(new Appointment
                        {
                            AppointmentId = newAppointmentId,
                            CustomerId = model.CustomerId,
                            PetId = petId,
                            ServiceId = serviceId,
                            StaffId = groomerId,
                            AppointmentDateTime = appointmentStartTime,
                            SpecialRequest = model.SpecialRequest,
                            Status = "Confirmed",
                            AdminId = currentAdminId,
                            DurationTime = service.DurationTime.Value,
                            CreatedAt = DateTime.Now
                        });
                    }
                }
                else // 默认行为：如果只有一只宠物，则使用StaffId；如果是多只宠物，则为 "Any" 模式
                {
                    // 单只宠物，指定了美容师
                    if (model.PetId.Count == 1 && (string.IsNullOrEmpty(model.GroomerMode) || model.GroomerMode == "one"))
                    {
                        var petId = model.PetId.First();
                        if (model.PetServiceMap.TryGetValue(petId, out var serviceId) && !string.IsNullOrEmpty(serviceId))
                        {
                            var service = await _context.Services.FindAsync(serviceId);
                            if (service == null || !service.DurationTime.HasValue)
                            {
                                TempData["ErrorMessage"] = "The selected service is invalid or has no duration.";
                                return RedirectToAction(nameof(Appointment));
                            }
    
                            var appointmentEndTime = appointmentStartTime.AddMinutes(service.DurationTime.Value);
                            string assignedGroomerId = null;
    
                            // Case 1: A specific groomer is selected
                            if (!string.IsNullOrEmpty(model.StaffId) && model.StaffId != "any")
                            {
                                bool isBusy = await _context.Appointments.AnyAsync(a =>
                                    a.StaffId == model.StaffId &&
                                    a.AppointmentDateTime < appointmentEndTime &&
                                    a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) > appointmentStartTime);
    
                                if (isBusy)
                                {
                                    var groomer = await _context.Staffs.FindAsync(model.StaffId);
                                    TempData["ErrorMessage"] = $"Groomer '{groomer?.Name}' is unavailable at the selected time.";
                                    return RedirectToAction(nameof(Appointment));
                                }
                                assignedGroomerId = model.StaffId;
                            }
                            else // Case 2: "Any Available Groomer" is selected
                            {
                                var availableGroomers = await _context.Staffs
                                    .Where(s => s.Position.Contains("Groomer"))
                                    .ToListAsync();
    
                                foreach (var groomer in availableGroomers)
                                {
                                    bool isBusy = await _context.Appointments.AnyAsync(a =>
                                        a.StaffId == groomer.UserId &&
                                        a.AppointmentDateTime < appointmentEndTime &&
                                        a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) > appointmentStartTime);
    
                                    if (!isBusy)
                                    {
                                        assignedGroomerId = groomer.UserId;
                                        break; // Found an available groomer
                                    }
                                }
    
                                if (assignedGroomerId == null)
                                {
                                    TempData["ErrorMessage"] = $"No groomers are available for the appointment at {appointmentStartTime:MMM dd, hh:mm tt}. Please choose another time.";
                                    return RedirectToAction(nameof(Appointment));
                                }
                            }
    
                            // Create the single appointment
                            while (usedNumbers.Contains(nextIdNum)) nextIdNum++;
                            var newAppointmentId = "AP" + nextIdNum.ToString("D3");
                            
                            string currentAdminId = HttpContext.Session.GetString("AdminId");
                            if (string.IsNullOrEmpty(currentAdminId))
                            {
                                TempData["ErrorMessage"] = "Your session has expired. Please log in again.";
                                return RedirectToAction(nameof(Appointment));
                            }
    
                            newAppointments.Add(new Appointment
                            {
                                AppointmentId = newAppointmentId,
                                CustomerId = model.CustomerId,
                                PetId = petId,
                                ServiceId = serviceId,
                                StaffId = assignedGroomerId,
                                AppointmentDateTime = appointmentStartTime,
                                SpecialRequest = model.SpecialRequest,
                                Status = "Confirmed",
                                AdminId = currentAdminId,
                                DurationTime = service.DurationTime.Value,
                                CreatedAt = DateTime.Now
                            });
    
                            // Since we've handled the single pet case completely, we can skip the rest of the logic.
                            // The final save logic is outside this 'else' block.
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "A service must be selected for the pet.";
                            return RedirectToAction(nameof(Appointment));
                        }
                    }
                    // This 'else' now correctly handles only the "Any" mode for multiple pets
                    else 
                    {
                    var sequentialStartTimeForAny = appointmentStartTime;

                    // 1. 获取所有美容师及其当天的预约
                    var groomers = await _context.Staffs
                        .Where(s => s.Position.Contains("Groomer"))
                        .Include(s => s.Appointments.Where(a => a.AppointmentDateTime.Value.Date == appointmentStartTime.Date))
                        .ThenInclude(a => a.Service)
                        .ToListAsync();

                    if (!groomers.Any())
                    {
                        TempData["ErrorMessage"] = "No groomers are configured in the system.";
                        return RedirectToAction(nameof(Appointment));
                    }

                    // 2. 模拟分配过程
                    foreach (var petId in model.PetId)
                    {
                        if (!model.PetServiceMap.TryGetValue(petId, out var serviceId)) continue;

                        var service = await _context.Services.FindAsync(serviceId);
                        if (service == null || !service.DurationTime.HasValue) continue;

                        var appointmentEndTime = sequentialStartTimeForAny.AddMinutes(service.DurationTime.Value);
                        string assignedGroomerId = null;

                        // 寻找一个能接这个单的美容师
                        foreach (var groomer in groomers.OrderBy(g => g.Appointments.Count)) // 优先分配给预约较少的美容师
                        {
                            // 检查该美容师的现有预约 + 已临时分配的预约
                            var existingBookings = groomer.Appointments.Select(a => (
                                Start: a.AppointmentDateTime.Value,
                                End: a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) // Use DurationTime from Appointment
                            ))
                                .ToList();

                            bool isAvailable = !existingBookings.Any(b =>
                                sequentialStartTimeForAny < b.End && appointmentEndTime > b.Start
                            );

                            if (isAvailable)
                            {
                                assignedGroomerId = groomer.UserId;
                                break; // 找到美容师，跳出循环
                            }
                        }

                        if (assignedGroomerId == null)
                        {
                            TempData["ErrorMessage"] = $"No groomers are available for one of the services starting at {sequentialStartTimeForAny:MMM dd, hh:mm tt}. Please choose another time or assign manually.";
                            return RedirectToAction(nameof(Appointment));
                        }

                        // 为这个宠物创建预约
                        while (usedNumbers.Contains(nextIdNum)) nextIdNum++;
                        var newAppointmentId = "AP" + nextIdNum.ToString("D3");
                        usedNumbers.Add(nextIdNum);

                        // Get Admin ID from session
                        string currentAdminId = HttpContext.Session.GetString("AdminId");
                        if (string.IsNullOrEmpty(currentAdminId))
                        {
                            TempData["ErrorMessage"] = "Your session has expired. Please log in again to create appointments.";
                            return RedirectToAction(nameof(Appointment));
                        }

                        var newAppt = new Appointment
                        {
                            AppointmentId = newAppointmentId,
                            CustomerId = model.CustomerId,
                            PetId = petId,
                            ServiceId = serviceId,
                            StaffId = assignedGroomerId,
                            AppointmentDateTime = sequentialStartTimeForAny,
                            SpecialRequest = model.SpecialRequest,
                            Status = "Confirmed",
                            AdminId = currentAdminId,
                            CreatedAt = DateTime.Now,
                            DurationTime = service.DurationTime.Value
                        };
                        newAppointments.Add(newAppt);
                        // 手动将新预约添加到美容师的预约列表中，以便下一次循环检查
                        // This is safe because we are adding the DurationTime to newAppt
                        groomers.First(g => g.UserId == assignedGroomerId).Appointments.Add(newAppt);

                    }

                }
                }
                if (newAppointments.Any())
                {
                    _context.Appointments.AddRange(newAppointments);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"{newAppointments.Count} appointment(s) have been successfully created!";
                }
                else
                {
                    TempData["WarningMessage"] = "No appointments were created. Please check the service details.";
                }
            }
            catch (Exception ex)
            {
                // 捕获任何意外的数据库错误或其他异常
                // 建议在这里记录错误日志 (log ex)
                TempData["ErrorMessage"] = "An unexpected error occurred while saving the appointments. Please contact support. Error: " + ex.Message;
            }
    
            return RedirectToAction(nameof(Appointment));
        }
        else if (actionType == "edit")
        {
            // 1. 验证传入的 AppointmentId 和 Status 是否有效
            if (string.IsNullOrEmpty(model.EditAppointmentId))
            {
                TempData["ErrorMessage"] = "Appointment ID was missing. Cannot update.";
                return RedirectToAction(nameof(Appointment));
            }

            // 2. 查找预约并验证状态转换规则
            var appointmentToUpdate = await _context.Appointments.FindAsync(model.EditAppointmentId);

            if (appointmentToUpdate == null)
            {
                TempData["ErrorMessage"] = "Appointment not found. It may have been deleted.";
                return RedirectToAction(nameof(Appointment));
            }
            // 强制规则：只允许从 "Confirmed" 变为 "Completed"
            if (appointmentToUpdate.Status != "Confirmed" || model.Status != "Completed")
            {
                TempData["ErrorMessage"] = "Invalid status change. Only 'Confirmed' appointments can be changed to 'Completed'.";
                // 重定向回当前筛选结果，并保持编辑状态
                return RedirectToAction(nameof(Appointment), new { status = model.FilterStatus, groomerid = model.FilterGroomerId, date = model.FilterDate?.ToString("yyyy-MM-dd"), editId = model.EditAppointmentId });
            }

            // 4. 更新预约信息
            appointmentToUpdate.Status = model.Status; // 允许更新状态

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Appointment {appointmentToUpdate.AppointmentId} has been successfully updated!";
        }
    
        return RedirectToAction(nameof(Appointment));
    }

    // 将此方法设为私有，因为它只应在控制器内部被调用
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidateAppointmentTime([FromBody] AppointmentViewModel model)
    {
        if (model.AppointmentDateTime == default || model.PetServiceMap == null || !model.PetServiceMap.Any())
        {
            return Json(new { isValid = true }); // Not enough info to validate, so don't block the user
        }

        var appointmentStartTime = model.AppointmentDateTime;

        // --- Single Groomer Validation ---
        // This now handles both "one" mode for multiple pets and the default mode for a single pet
        if ((model.GroomerMode == "one" || string.IsNullOrEmpty(model.GroomerMode)) && !string.IsNullOrEmpty(model.StaffId) && model.StaffId != "any")
        {
            double totalDuration = 0;
            foreach (var petServicePair in model.PetServiceMap)
            {
                var service = await _context.Services.FindAsync(petServicePair.Value);
                if (service?.DurationTime.HasValue ?? false)
                {
                    totalDuration += service.DurationTime.Value;
                }
            }

            var finalEndTime = appointmentStartTime.AddMinutes(totalDuration);

            // Check if it exceeds closing time
            if (finalEndTime.TimeOfDay > new TimeSpan(16, 30, 0))
            {
                return Json(new { isValid = false, field = "AppointmentDateTime", message = $"Total duration exceeds closing time. Finishes at {finalEndTime:hh:mm tt}." });
            }

            // Check for conflicts with existing appointments
            bool isBusy = await _context.Appointments.AnyAsync(a =>
                a.StaffId == model.StaffId &&
                a.AppointmentDateTime < finalEndTime &&
                a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) > appointmentStartTime);

            if (isBusy)
            {
                return Json(new { isValid = false, field = "StaffId", message = "This groomer is unavailable at the selected time." });
            }
        }
        // --- Individual Groomer Validation ---
        else if (model.GroomerMode == "individual" && model.PetGroomerMap != null)
        {
            var groomerChecks = new Dictionary<string, List<double>>();

            foreach (var petServicePair in model.PetServiceMap)
            {
                if (model.PetGroomerMap.TryGetValue(petServicePair.Key, out var groomerId) && !string.IsNullOrEmpty(groomerId))
                {
                    var service = await _context.Services.FindAsync(petServicePair.Value);
                    if (service?.DurationTime.HasValue ?? false)
                    {
                        if (!groomerChecks.ContainsKey(groomerId))
                        {
                            groomerChecks[groomerId] = new List<double>();
                        }
                        groomerChecks[groomerId].Add(service.DurationTime.Value);
                    }
                }
            }

            foreach (var groomerCheck in groomerChecks)
            {
                var groomerId = groomerCheck.Key;
                var totalDurationForGroomer = groomerCheck.Value.Sum();
                var finalEndTime = appointmentStartTime.AddMinutes(totalDurationForGroomer);

                if (finalEndTime.TimeOfDay > new TimeSpan(16, 30, 0))
                {
                    return Json(new { isValid = false, field = "StaffId", message = $"Services for one groomer exceed closing time." });
                }

                bool isBusy = await _context.Appointments.AnyAsync(a =>
                    a.StaffId == groomerId &&
                    a.AppointmentDateTime < finalEndTime &&
                    a.AppointmentDateTime.Value.AddMinutes(a.DurationTime ?? 0) > appointmentStartTime);

                if (isBusy)
                {
                    var groomer = await _context.Staffs.FindAsync(groomerId);
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
        var gifts = await _context.RedeemGifts.OrderByDescending(g => g.GiftId).ToListAsync();
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
            var allGiftIds = await _context.RedeemGifts
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
                var list = await _context.RedeemGifts
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

            _context.RedeemGifts.Add(gift);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $" Gift '{gift.Name}' created successfully!";
            return RedirectToAction(nameof(RedeemGift));
        }

        // --- EDIT ---
        else if (actionType == "edit")
        {
            if (editGiftId == null) return NotFound();
            var dbGift = await _context.RedeemGifts.FindAsync(editGiftId);
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

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $" Gift '{gift.Name}' updated successfully!";
            return RedirectToAction(nameof(RedeemGift));
        }

        // --- DELETE ---
        else if (actionType == "delete")
        {
            if (deleteGiftId == null) return NotFound();
            var dbGift = await _context.RedeemGifts.FindAsync(deleteGiftId);
            if (dbGift == null) return NotFound();
            _context.RedeemGifts.Remove(dbGift);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = " Gift deleted successfully!";
            return RedirectToAction(nameof(RedeemGift));
        }

        // --- FALLBACK ---
        var gifts = await _context.RedeemGifts.OrderByDescending(g => g.GiftId).ToListAsync();
        return View(gifts);
    }

    // ========== SERVICE
    // GET: List all services
    public async Task<IActionResult> Service(string ServiceName, string Category, string Status, string editId)
    {
        ViewData["ActivePage"] = "Service";
        var query = _context.Services
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
        var categories = await _context.ServiceCategories.OrderBy(c => c.CategoryId).ToListAsync();
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
            var allServiceIds = await _context.Services
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
            _context.Services.Add(service);

            // ===== CREATE SSC =====
            var selectedCats = SelectedCategories?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList() ?? new List<string>();
            TempData["Debug_RawSelectedCategories_Create"] = SelectedCategories != null && SelectedCategories.Any() ? string.Join(", ", SelectedCategories) : "None (raw)";
            TempData["Debug_FilteredSelectedCats_Create"] = selectedCats.Any() ? string.Join(", ", selectedCats) : "None (filtered)";

            if (!selectedCats.Any())
            {
                // No categories selected, just save the service
                await _context.SaveChangesAsync();
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

            _context.ServiceServiceCategories.AddRange(sscEntries);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Service '{service.Name}' created successfully!";

        }

        // ================= EDIT =================
        else if (actionType == "edit")
        {
            if (string.IsNullOrEmpty(editServiceId))
                return NotFound();

            var dbService = await _context.Services
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
                _context.ServiceServiceCategories.RemoveRange(dbService.ServiceServiceCategories);
                await _context.SaveChangesAsync();
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

                _context.ServiceServiceCategories.AddRange(sscEntries);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Service '{service.Name}' updated successfully!";
            return RedirectToAction(nameof(Service));
        }

        // ================= DELETE =================
        else if (actionType == "delete")
        {
            if (string.IsNullOrEmpty(deleteServiceId))
                return NotFound();

            var dbService = await _context.Services
 .Include(s => s.ServiceServiceCategories)
 .FirstOrDefaultAsync(s => s.ServiceId == deleteServiceId);
            if (dbService == null)
                return NotFound();

            // Remove related junction rows first to avoid FK constraint errors
            if (dbService.ServiceServiceCategories != null && dbService.ServiceServiceCategories.Any())
            {
                _context.ServiceServiceCategories.RemoveRange(dbService.ServiceServiceCategories);
            }

            _context.Services.Remove(dbService);
            await _context.SaveChangesAsync();

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

            var dbAppointment = await _context.Appointments.FindAsync(appointmentId);

            if (dbAppointment == null)
            {
                return Json(new { success = false, message = "Appointment not found." });
            }

            _context.Appointments.Remove(dbAppointment);
            await _context.SaveChangesAsync();

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
            customers = await _context.Customers
                .Where(c => c.Status == "Active")
                .OrderBy(c => c.Name)
                .Select(c => new { id = c.UserId, text = c.Name + " (" + c.Phone + ")" })
                .Take(20)
                .ToListAsync<object>();
        }
        else
        {
            customers = await _context.Customers
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

            var bookingCounts = await _context.Appointments
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

            var appointments = await _context.Appointments
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

            var appointments = await _context.Appointments
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

    var services = _context.Services
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
            var allStaffIds = await _context.Staffs
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
            _context.Staffs.Add(staff);
            await _context.SaveChangesAsync();

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

            var dbStaff = await _context.Staffs.FindAsync(editStaffId);
            if (dbStaff == null)
            {
                return Json(new { success = false, errors = new Dictionary<string, string> { { "General", "Staff not found." } } });
            }

            // ========== VALIDATE INPUTS USING SERVICES =========
            var errors = await ValidateStaffAsync(staff, editStaffId);

            // If there are validation errors, return them
            if (errors.Any())
            {
                return Json(new { success = false, errors = errors });
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
                            Console.WriteLine($"[WARNING] Failed to delete old photo: {ex.Message}");
                        }
                    }
                }

                dbStaff.Photo = "/uploads/" + fileName;
            }

            await _context.SaveChangesAsync();

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
                    isDuplicate = await _context.Staffs.AnyAsync(s => s.IC == value);
                    break;

                case "email":
                    isDuplicate = await _context.Staffs.AnyAsync(s => s.Email == value);
                    break;

                case "phone":
                    // Format phone number before checking using PhoneService
                    string formattedPhone = _phoneService.FormatPhoneNumber(value);
                    isDuplicate = await _context.Staffs.AnyAsync(s => s.Phone == formattedPhone);
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
                    isDuplicate = await _context.Staffs.AnyAsync(s => s.IC == value && s.UserId != excludeUserId);
                    break;

                case "email":
                    isDuplicate = await _context.Staffs.AnyAsync(s => s.Email == value && s.UserId != excludeUserId);
                    break;

                case "phone":
                    // Format phone number before checking using PhoneService
                    string formattedPhone = _phoneService.FormatPhoneNumber(value);
                    isDuplicate = await _context.Staffs.AnyAsync(s => s.Phone == formattedPhone && s.UserId != excludeUserId);
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
            var allJunctions = _context.ServiceServiceCategories.ToList();
            if (allJunctions.Any())
            {
                _context.ServiceServiceCategories.RemoveRange(allJunctions);
            }

            // Then remove services
            var allServices = _context.Services.ToList();
            if (allServices.Any())
            {
                _context.Services.RemoveRange(allServices);
            }

            await _context.SaveChangesAsync();
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

            var dbService = await _context.Services
            .Include(s => s.ServiceServiceCategories)
            .FirstOrDefaultAsync(s => s.ServiceId == serviceId);

            if (dbService == null)
                return Json(new { success = false, message = "Service not found." });

            // Remove junction rows first
            if (dbService.ServiceServiceCategories != null && dbService.ServiceServiceCategories.Any())
            {
                _context.ServiceServiceCategories.RemoveRange(dbService.ServiceServiceCategories);
            }

            _context.Services.Remove(dbService);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Service deleted successfully.", serviceId = serviceId });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DeleteServiceAjax failed: {ex.Message}");
            // Return a JSON error so all code paths return a value
            return Json(new { success = false, message = "Failed to delete service." });
        }
    }

    private async Task<Dictionary<string, string>> ValidateStaffAsync(Models.Staff staff, string staffIdToExclude = null)
    {
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
        else if (await _context.Staffs.AnyAsync(s => s.IC == staff.IC && s.UserId != staffIdToExclude))
            errors["IC"] = "This IC number is already registered.";

        // Validate Email using ValidationService
        if (string.IsNullOrWhiteSpace(staff.Email))
            errors["Email"] = "Email cannot be empty.";
        else if (!_validationService.ValidateEmail(staff.Email))
            errors["Email"] = "Please enter a valid email address.";
        else if (await _context.Staffs.AnyAsync(s => s.Email == staff.Email && s.UserId != staffIdToExclude))
            errors["Email"] = "This email address is already registered.";

        // Format and validate Phone using PhoneService
        string formattedPhoneNumber = _phoneService.FormatPhoneNumber(staff.Phone);

        if (string.IsNullOrWhiteSpace(staff.Phone))
            errors["Phone"] = "Phone number cannot be empty.";
        else if (!_phoneService.ValidatePhoneFormat(formattedPhoneNumber))
            errors["Phone"] = "Phone number must be in format 01X-XXXXXXX or 01X-XXXXXXXX (e.g., 012-1234567).";
        else if (await _context.Staffs.AnyAsync(s => s.Phone == formattedPhoneNumber && s.UserId != staffIdToExclude))
            errors["Phone"] = "This phone number is already registered.";
        else
            staff.Phone = formattedPhoneNumber; // Update staff object with formatted phone if valid

        // Validate Experience Year using ValidationService
        if (staff.ExperienceYear.HasValue && !_validationService.ValidateExperienceYear(staff.ExperienceYear))
            errors["ExperienceYear"] = "Experience must be between 0-50 years.";

        // Validate Position using ValidationService
        if (string.IsNullOrWhiteSpace(staff.Position))
            errors["Position"] = "Please select a position.";
        else if (!_validationService.ValidatePosition(staff.Position))
            errors["Position"] = "Please select a valid position.";

        return errors;
    }

        private async Task<int> GetNextAvailableNumericId(string prefix, HashSet<string> assignedIdsInCurrentBatch)

        {

            var allExistingIds = await _context.ServiceServiceCategories

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

        [HttpGet]
        [Route("Admin/Home/GetPetsByCustomerId")]
        public async Task<IActionResult> GetPetsByCustomerId(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                return Json(new List<object>());
            }

            // Order in the database by Name and Type, then project on client to avoid EF translation of string.Format
            var petsFromDb = await _context.Pets
                .Where(p => p.CustomerId == customerId)
                .OrderBy(p => p.Name)
                .ThenBy(p => p.Type)
                .Select(p => new { p.PetId, p.Name, p.Type })
                .ToListAsync();

            var pets = petsFromDb.Select(p => new { id = p.PetId, text = p.Name, type = p.Type }).ToList();

            return Json(pets);
        }
}
