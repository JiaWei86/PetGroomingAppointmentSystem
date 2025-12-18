using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Generators;
using PetGroomingAppointmentSystem.Areas.Admin.Controllers;
using PetGroomingAppointmentSystem.Areas.Staff.ViewModels;
using PetGroomingAppointmentSystem.Models;

namespace PetGroomingAppointmentSystem.Areas.Staff.Controllers
{
    [Area("Staff")]
    [StaffOnly]
    public class HomeController : Controller
    {
        private readonly DB _db;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public HomeController(DB context, IWebHostEnvironment webHostEnvironment)
        {
            _db = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["ActivePage"] = "Appointment";

            // Get the current logged-in staff member from session (not claims)
            var staffId = HttpContext.Session.GetString("StaffId");
            
            // If no session, redirect to login
            if (string.IsNullOrEmpty(staffId))
            {
                return RedirectToAction("Login", "Auth", new { area = "Staff" });
            }

            // Fetch appointments for this staff member
            var now = DateTime.Now;
            var calendarStartDate = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            var calendarEndDate = new DateTime(now.Year, now.Month, 1).AddMonths(2);

            var appointmentsForCalendar = await _db.Appointments
                .Where(a => a.StaffId == staffId && a.AppointmentDateTime >= calendarStartDate && a.AppointmentDateTime < calendarEndDate)
                .Include(a => a.Pet)
                .Include(a => a.Staff)
                .Include(a => a.Service)
                .Select(a => new PetGroomingAppointmentSystem.Models.ViewModels.CalendarAppointmentModel
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

            // Create a simple view model for the staff calendar
            var viewModel = new CalendarAppointmentModel
            {
                AppointmentsForCalendar = appointmentsForCalendar.Cast<PetGroomingAppointmentSystem.Models.ViewModels.CalendarAppointmentModel>().ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAppointmentStatus([FromBody] UpdateAppointmentRequest request)
        {
            var staffId = HttpContext.Session.GetString("StaffId");

            if (string.IsNullOrEmpty(staffId))
            {
                return Json(new { success = false, message = "Unauthorized access." });
            }

            // Validate appointment ID and status
            if (string.IsNullOrEmpty(request?.AppointmentId) || string.IsNullOrEmpty(request?.NewStatus))
            {
                return Json(new { success = false, message = "Invalid appointment ID or status." });
            }

            try
            {
                var appointment = await _db.Appointments
                    .Include(a => a.Service)
                    .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId);

                if (appointment == null)
                {
                    return Json(new { success = false, message = "Appointment not found." });
                }

                // Verify the staff member owns this appointment
                if (appointment.StaffId != staffId)
                {
                    return Json(new { success = false, message = "You are not authorized to update this appointment." });
                }

                // Validate status transitions - use case-insensitive comparison
                var validStatuses = new[] { "Confirmed", "Completed", "Cancelled" };
                if (!validStatuses.Contains(request.NewStatus))
                {
                    return Json(new { success = false, message = "Invalid status value." });
                }

                // Only allow transitions from Confirmed -> Completed (case-insensitive comparison)
                var currentStatus = appointment.Status?.ToLower() ?? "";
                if (currentStatus != "confirmed" || request.NewStatus.ToLower() != "completed")
                {
                    return Json(new { success = false, message = "Invalid status change. Only 'Confirmed' appointments can be marked as 'Completed'." });
                }

                // ========== NEW VALIDATION: Check appointment date and time =========
                if (!appointment.AppointmentDateTime.HasValue)
                {
                    return Json(new { success = false, message = "Appointment has no valid date/time set." });
                }

                var appointmentDate = appointment.AppointmentDateTime.Value;
                var currentDateTime = DateTime.Now;
                var serviceDuration = appointment.DurationTime ?? (appointment.Service?.DurationTime ?? 0);

                // Check if the appointment date matches today
                if (appointmentDate.Date != currentDateTime.Date)
                {
                    return Json(new 
                    { 
                        success = false, 
                        message = $"Appointment can only be marked as completed on its scheduled date ({appointmentDate:MMM dd, yyyy}). Today's date is {currentDateTime:MMM dd, yyyy}." 
                    });
                }

                // Calculate the appointment end time (appointment time + service duration)
                var appointmentEndTime = appointmentDate.AddMinutes(serviceDuration);

                // Check if the current time is after the appointment end time
                if (currentDateTime < appointmentEndTime)
                {
                    var timeRemaining = appointmentEndTime - currentDateTime;
                    var minutesRemaining = (int)Math.Ceiling(timeRemaining.TotalMinutes);
                    return Json(new 
                    { 
                        success = false, 
                        message = $"This appointment is scheduled to end at {appointmentEndTime:hh:mm tt}. You can mark it as completed only after that time. Time remaining: {minutesRemaining} minute(s)." 
                    });
                }

                // ========== END OF NEW VALIDATION ==========

                appointment.Status = request.NewStatus;
                _db.Appointments.Update(appointment);
                await _db.SaveChangesAsync();

                return Json(new { success = true, message = $"Appointment status updated to {request.NewStatus}.", appointmentId = request.AppointmentId, newStatus = request.NewStatus.ToLower() });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating appointment: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = "An error occurred while updating the appointment: " + ex.Message });
            }
        }

        public async Task<IActionResult> Profile()
        {
            var staffId = HttpContext.Session.GetString("StaffId");

            if (string.IsNullOrEmpty(staffId))
            {
                return RedirectToAction("Login", "Auth", new { area = "Staff" });
            }

            var staffMember = await _db.Staffs
                .FirstOrDefaultAsync(s => s.UserId == staffId);

            if (staffMember == null)
            {
                TempData["ErrorMessage"] = "Staff member not found.";
                return NotFound();
            }

            return View(staffMember);
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(PetGroomingAppointmentSystem.Models.Staff model)
        {
            var staff = _db.Staffs.FirstOrDefault(s => s.UserId == model.UserId);
            if (staff == null) return NotFound();

            staff.Name = model.Name;

            // Handle photo upload with file size validation
            if (Request.Form.Files.Count > 0)
            {
                var photoFile = Request.Form.Files[0];
                if (photoFile.Length > 0)
                {
                    // Maximum file size: 5 MB
                    const long maxFileSize = 5 * 1024 * 1024;

                    if (photoFile.Length > maxFileSize)
                    {
                        var fileSizeMB = (photoFile.Length / 1024.0 / 1024.0).ToString("F2");
                        TempData["ErrorMessage"] = $"Photo file is too large ({fileSizeMB}MB). Maximum allowed size is 5MB.";
                        return RedirectToAction("Profile");
                    }

                    // Validate file type (allow only image files)
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(photoFile.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        TempData["ErrorMessage"] = "Invalid file type. Only image files (.jpg, .jpeg, .png, .gif, .webp) are allowed.";
                        return RedirectToAction("Profile");
                    }

                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "staff");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + photoFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await photoFile.CopyToAsync(fileStream);
                    }

                    staff.Photo = "/uploads/staff/" + uniqueFileName;
                }
            }

            if (!string.IsNullOrEmpty(model.Password))
            {
                staff.Password = model.Password;
            }

            _db.SaveChanges();
            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }
    }

    // Add this request class at the end of the file (before the closing brace)
    public class UpdateAppointmentRequest
    {
        public string AppointmentId { get; set; }
        public string NewStatus { get; set; }
    }
}