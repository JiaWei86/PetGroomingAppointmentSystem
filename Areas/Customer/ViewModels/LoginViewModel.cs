using System.ComponentModel.DataAnnotations;

namespace PetGroomingAppointmentSystem.Areas.Customer.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Phone number is required.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        // ✅ 新增：Remember Me checkbox
        public bool RememberMe { get; set; }
    }
}