namespace PetGroomingAppointmentSystem.Areas.Admin.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int GroomerId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string PetName { get; set; }
        public string PetType { get; set; }
        public string ServiceType { get; set; }
        public string Status { get; set; } // Confirmed, Pending, Completed, Cancelled
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual Groomer Groomer { get; set; }
    }
}