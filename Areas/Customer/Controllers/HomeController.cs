using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetGroomingAppointmentSystem.Models;
using PetGroomingAppointmentSystem.Areas.Customer.ViewModels;
using System.Security.Claims;
using System.Text;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;

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

        // GET: Appointment History Data
        [HttpGet]
        public IActionResult GetAppointmentHistory()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
                if (customer == null)
                    return NotFound();

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
                    return Unauthorized();

                var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
                if (customer == null)
                    return NotFound();

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
                    return Unauthorized();

                var customer = _db.Customers.FirstOrDefault(c => c.UserId == userId);
                if (customer == null)
                    return NotFound();

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
                var document = new Document();
                var writer = PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                // Set fonts
                var titleFont = new Font(Font.FontFamily.HELVETICA, 16, Font.BOLD);
                var headerFont = new Font(Font.FontFamily.HELVETICA, 12, Font.BOLD);
                var normalFont = new Font(Font.FontFamily.HELVETICA, 10);
                var smallFont = new Font(Font.FontFamily.HELVETICA, 8);

                // Title
                var title = new Paragraph("PET GROOMING APPOINTMENT RECEIPT", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);

                var space = new Paragraph(" ");
                document.Add(space);

                // Receipt Info
                var receiptInfo = new Paragraph($"Receipt #: {appointment.AppointmentId}", normalFont);
                receiptInfo.Alignment = Element.ALIGN_LEFT;
                document.Add(receiptInfo);

                var dateInfo = new Paragraph($"Date Issued: {DateTime.Now:MMM dd, yyyy HH:mm tt}", normalFont);
                document.Add(dateInfo);
                document.Add(space);

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
                document.Add(space);

                // Appointment Section
                var appointmentHeader = new Paragraph("APPOINTMENT DETAILS", headerFont);
                document.Add(appointmentHeader);

                var appointmentTable = new PdfPTable(2);
                appointmentTable.SetWidths(new float[] { 30, 70 });
                appointmentTable.AddCell(new PdfPCell(new Phrase("Date:", normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.AppointmentDateTime?.ToString("MMM dd, yyyy"), normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase("Time:", normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.AppointmentDateTime?.ToString("hh:mm tt"), normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase("Pet(s):", normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Pet?.Name ?? "N/A", normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase("Groomer:", normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Staff?.Name ?? "Not assigned", normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase("Service:", normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Service?.Name, normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase("Duration:", normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase($"{appointment.DurationTime} minutes", normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase("Status:", normalFont)) { Border = 0 });
                appointmentTable.AddCell(new PdfPCell(new Phrase(appointment.Status, normalFont)) { Border = 0 });

                document.Add(appointmentTable);

                if (!string.IsNullOrEmpty(appointment.SpecialRequest))
                {
                    document.Add(space);
                    var notesHeader = new Paragraph("SPECIAL REQUESTS", headerFont);
                    document.Add(notesHeader);
                    var notes = new Paragraph(appointment.SpecialRequest, normalFont);
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

                var footer = new Paragraph("Thank you for choosing our service!", smallFont);
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