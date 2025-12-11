using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Areas.Admin.Controllers; 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace PetGroomingAppointmentSystem.Areas.Admin.Controllers
{                                   
    [Area("Admin")]
    [AdminOnly] 
    public class HomeController : Controller
    {
        private readonly DB _db;

        public HomeController(DB db)
        {
            _db = db;
        }
        
        // ========== DASHBOARD ==========
        public IActionResult Index()
        {
            ViewData["ActivePage"] = "Dashboard";
            return View();
        }

        // ========== GROOMER ==========
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
                // ========== SMART ID GENERATION ==========
                // Get all existing Staff IDs and sort them
                var allStaffIds = await _db.Staffs
                    .Select(s => s.UserId)
                    .OrderBy(id => id)
                    .ToListAsync();

                string newStaffId;

                if (!allStaffIds.Any())
                {
                    // No staff exists, start from S001
                    newStaffId = "S001";
                }
                else
                {
                    // Extract numeric parts and find gaps
                    var usedNumbers = allStaffIds
                        .Select(id => int.Parse(id.Substring(1)))
                        .OrderBy(n => n)
                        .ToList();

                    // Check for gaps starting from 1
                    int nextNumber = 1;
                    bool foundGap = false;

                    foreach (var num in usedNumbers)
                    {
                        if (num != nextNumber)
                        {
                            // Found a gap! Use this number
                            foundGap = true;
                            break;
                        }
                        nextNumber++;
                    }

                    // If no gap found, use max + 1
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
                staff.CreatedAt = DateTime.Now;
                staff.AdminUserId = currentAdminId;
                staff.Description = staff.Description ?? "";

                // Add to Staffs (EF will handle Users table)
                _db.Staffs.Add(staff);
                await _db.SaveChangesAsync();

                return RedirectToAction(nameof(Groomer));
            }

            // --- EDIT ---
            else if (actionType == "edit")
            {
                if (string.IsNullOrEmpty(editStaffId)) return NotFound();
                
                var dbStaff = await _db.Staffs.FindAsync(editStaffId);
                if (dbStaff == null) return NotFound();

                // Update fields
                dbStaff.Name = staff.Name;
                dbStaff.Email = staff.Email;
                dbStaff.Phone = staff.Phone;
                dbStaff.IC = staff.IC;
                dbStaff.Description = staff.Description;
                dbStaff.ExperienceYear = staff.ExperienceYear;
                dbStaff.Position = staff.Position;

                // Handle photo upload in edit
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
                    
                    dbStaff.Photo = "/uploads/" + fileName;
                }

                await _db.SaveChangesAsync();
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
                
                return RedirectToAction(nameof(Groomer));
            }

            // Fallback
            var allGroomers = await _db.Staffs.OrderByDescending(s => s.UserId).ToListAsync();
            return View(allGroomers);
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
                // Get all existing Gift IDs and sort them
                var allGiftIds = await _db.RedeemGifts
                    .Select(g => g.GiftId)
                    .OrderBy(id => id)
                    .ToListAsync();

                string newGiftId;

                if (!allGiftIds.Any())
                {
                    // No gifts exist, start from G001
                    newGiftId = "G001";
                }
                else
                {
                    // Extract numeric parts and find gaps
                    var usedNumbers = allGiftIds
                        .Select(id => int.Parse(id.Substring(1)))
                        .OrderBy(n => n)
                        .ToList();

                    // Check for gaps starting from 1
                    int nextNumber = 1;
                    bool foundGap = false;

                    foreach (var num in usedNumbers)
                    {
                        if (num != nextNumber)
                        {
                            // Found a gap! Use this number
                            foundGap = true;
                            break;
                        }
                        nextNumber++;
                    }

                    // If no gap found, use max + 1
                    if (!foundGap)
                    {
                        nextNumber = usedNumbers.Max() + 1;
                    }

                    newGiftId = "G" + nextNumber.ToString("D3");
                }

                gift.GiftId = newGiftId;
                gift.AdminId = HttpContext.Session.GetString("AdminId");

                Console.WriteLine("ADMIN ID = " + gift.AdminId);
                Debug.WriteLine("ADMIN ID = " + gift.AdminId);

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
                return RedirectToAction(nameof(RedeemGift));
            }

            // --- FALLBACK ---
            var gifts = await _db.RedeemGifts.OrderByDescending(g => g.GiftId).ToListAsync();
            return View(gifts);
        }

        public IActionResult Service()
        {
            ViewData["ActivePage"] = "Service";
            return View();
        }



        public IActionResult reports()
        {
            ViewData["ActivePage"] = "reports";
            return View();
        }
    }
}