using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetGroomingAppointmentSystem.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PetGroomingAppointmentSystem.Areas.Admin.Controllers
{
    [Area("Admin")]
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
        public IActionResult Groomer()
        {
            ViewData["ActivePage"] = "Groomer";
            return View();
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
    return View(gifts); // Use Areas/Admin/Views/Home/RedeemGift.cshtml
}

        // CREATE (POST) — if you add an inline form to your RedeemGift.cshtml
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
                // 找出数据库里当前最大的 GiftId
                var lastGift = await _db.RedeemGifts
                .OrderByDescending(g => g.GiftId)
                .FirstOrDefaultAsync();

                string newGiftId = (lastGift == null)
                    ? "G0001"
                    : "G" + (int.Parse(lastGift.GiftId.Substring(1)) + 1).ToString("D4");

                gift.GiftId = newGiftId;

                // 取得 AdminId
                gift.AdminId = HttpContext.Session.GetString("AdminId");


                Console.WriteLine("ADMIN ID = " + gift.AdminId);
                Debug.WriteLine("ADMIN ID = " + gift.AdminId);


                // 如果 AdminId 为 null → 显示错误并不保存
                if (string.IsNullOrEmpty(gift.AdminId))
                {
                    ViewData["Error"] = "❌ Unable to save: Admin is not logged in. Please login again.";

                    var list = await _db.RedeemGifts
                        .OrderByDescending(g => g.GiftId)
                        .ToListAsync();

                    return View(list);
                }

                // 上传图片
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
                    gift.Photo = "/uploads/placeholder.png"; // 默认图片
                }
      
                _db.RedeemGifts.Add(gift);
                _db.SaveChanges();
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


        // ========== OTHER ACTIONS ==========
        public IActionResult customerIndex()
        {
            ViewData["ActivePage"] = "customerIndex";
            return View();
        }

        public IActionResult reports()
        {
            ViewData["ActivePage"] = "reports";
            return View();
        }





    }
}