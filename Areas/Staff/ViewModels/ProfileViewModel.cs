using System.ComponentModel.DataAnnotations;
using PetGroomingAppointmentSystem.Areas.Customer.ViewModels;
using PetGroomingAppointmentSystem.Models;

namespace PetGroomingAppointmentSystem.Areas.Staff.ViewModels
{
    public class ProfileViewModel
    {
        public string Name { get; set; }

        public string IC { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public int? ExperienceYear { get; set; }
        public string Position { get; set; }
        public string Description { get; set; }

        public IFormFile PhotoUpload { get; set; }

    }
}



