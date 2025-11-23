using Microsoft.EntityFrameworkCore;
namespace PetGroomingAppointmentSystem.Areas.Customer.Models
{
    public class DB(DbContextOptions options) : DbContext(options)
    {
    }
}
