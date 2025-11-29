namespace PetGroomingAppointmentSystem.Areas.Admin.Models
{
    public class Groomer
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Specialty { get; set; }
        public string Status { get; set; } // Active, On Leave, Inactive
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}