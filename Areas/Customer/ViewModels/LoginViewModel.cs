using System.ComponentModel.DataAnnotations;

namespace PetGroomingAppointmentSystem.Areas.Customer.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(20, MinimumLength = 10, ErrorMessage = "Phone number must be valid")]
        [RegularExpression(@"^01[0-9]-?[0-9]{7,8}$", ErrorMessage = "Phone number must start with 01X and contain 7-8 digits")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}