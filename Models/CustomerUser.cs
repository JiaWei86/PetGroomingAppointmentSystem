namespace PetGroomingAppointmentSystem.Models
{
    public class CustomerUser
    {
        public int Id { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Name { get; set; }
        public required string IC { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
}