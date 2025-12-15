using System.ComponentModel.DataAnnotations;
using PetGroomingAppointmentSystem.Areas.Customer.ViewModels;

namespace PetGroomingAppointmentSystem.Areas.Admin.ViewModels;

public class GroomerViewModel
{
    [Required(ErrorMessage = "Full name cannot be empty")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Name must be between 3-200 characters")]
    [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Name must contain only letters and spaces")]
    public string Name { get; set; }

    [Required(ErrorMessage = "IC number cannot be empty")]
    [StringLength(20, MinimumLength = 14, ErrorMessage = "IC number must be in format xxxxxx-xx-xxxx")]
    [RegularExpression(@"^\d{6}-\d{2}-\d{4}$", ErrorMessage = "IC number must be in format xxxxxx-xx-xxxx (e.g., 123456-78-9012)")]
    [ValidMalaysianIC]
    public string IC { get; set; }

    [Required(ErrorMessage = "Email cannot be empty")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    [StringLength(150, ErrorMessage = "Email must not exceed 150 characters")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Phone number cannot be empty")]
    [StringLength(20, MinimumLength = 11, ErrorMessage = "Phone number must be in format 01X-XXXXXXXX or 01X-XXXXXXXXX")]
    [RegularExpression(@"^01[0-9]-?[0-9]{7,8}$", ErrorMessage = "Phone number must start with 01X and contain 8-9 digits (e.g., 0121234567 or 012-1234567)")]
    public string Phone { get; set; }

    [Range(0, 50, ErrorMessage = "Experience must be between 0-50 years")]
    public int? ExperienceYear { get; set; }

    [StringLength(100, ErrorMessage = "Position must not exceed 100 characters")]
    public string Position { get; set; }

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string Description { get; set; }

    public IFormFile PhotoUpload { get; set; }

    // Properties used for form actions
    public string editStaffId { get; set; }
    public string deleteStaffId { get; set; }
}