using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetGroomingAppointmentSystem.Models;

#nullable disable

/// <summary>
/// Single DB.cs: DbContext + all entity classes (data-annotation only)
/// Paste this file into Models folder.
/// </summary>
public class DB : DbContext
{
    public DB(DbContextOptions<DB> options) : base(options) { }

    // DbSets (plural)
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Staff> Staffs { get; set; }
    public DbSet<Customer> Customers { get; set; }

    public DbSet<ServiceCategory> ServiceCategories { get; set; }
    public DbSet<Service> Services { get; set; }

    public DbSet<Pet> Pets { get; set; }
    public DbSet<RedeemGift> RedeemGifts { get; set; }

    public DbSet<Appointment> Appointments { get; set; }

    public DbSet<CustomerRedeemGift> CustomerRedeemGifts { get; set; }
}

/* =========================
   USER (Parent Base Class) Table
   ========================= */
public class User
{
    [Key, MaxLength(10)]
    public string UserId { get; set; }   // e.g., U001

    [MaxLength(200)]
    public string Name { get; set; }

    // IC: stored as string for safety (leading zeros)
    [MaxLength(20)]
    public string IC { get; set; }

    [MaxLength(150)]
    [EmailAddress]
    public string Email { get; set; }

    [MaxLength(20)]
    public string Phone { get; set; }    // Malaysia format

    [Required, MaxLength(200)]
    public string Password { get; set; }

    // store path to photo (string) — change to byte[] for BLOB if needed
    [MaxLength(300)]
    public string Photo { get; set; }

    // enum-like role values: "admin", "staff", "customer"
    [Required, MaxLength(20)]
    public string Role { get; set; }

    public DateTime? CreatedAt { get; set; }

    // convenience navigations
    public List<Pet> Pets { get; set; } = new List<Pet>();
    public List<Appointment> Appointments { get; set; } = new List<Appointment>();
}

/* =========================
   ROLE-SPECIFIC CHILD CLASSES (Inherit from User)
   ========================= */

[Table("Admins")]
public class Admin : User
{

    // Admin controls
    public List<ServiceCategory> ServiceCategories { get; set; } = new List<ServiceCategory>();
    public List<Service> Services { get; set; } = new List<Service>();
    public List<RedeemGift> RedeemGifts { get; set; } = new List<RedeemGift>();
    public List<Pet> ManagedPets { get; set; } = new List<Pet>();
    public List<Staff> ManagedStaff { get; set; } = new List<Staff>();
}

[Table("Staffs")]
public class Staff : User
{
    [MaxLength(500)]
    public string Description { get; set; }

    public int? ExperienceYear { get; set; }

    [MaxLength(100)]
    public string Position { get; set; }

    // Overrides inherited Appointments (for staff-specific appointments only)
    [NotMapped]
    public new List<Appointment> Appointments { get; set; } = new List<Appointment>();
}

[Table("Customers")]
public class Customer : User
{
    public int LoyaltyPoint { get; set; } = 0;

    [MaxLength(50)]
    public string Status { get; set; }     // e.g., "active", "blocked"

    public DateTime? RegisteredDate { get; set; }

    // Overrides inherited Pets and Appointments (for customer-specific records)
    [NotMapped]
    public new List<Pet> Pets { get; set; } = new List<Pet>();
    
    [NotMapped]
    public new List<Appointment> Appointments { get; set; } = new List<Appointment>();
    
    public List<CustomerRedeemGift> Redeems { get; set; } = new List<CustomerRedeemGift>();
}

/* =========================
   MAIN BUSINESS TABLES
   ========================= */

public class ServiceCategory
{
    [Key, MaxLength(10)]
    public string CategoryId { get; set; }  // e.g., C001

    [MaxLength(200)]
    public string Name { get; set; }

    // FK to Admin
    [MaxLength(10)]
    public string AdminId { get; set; }

    [ForeignKey(nameof(AdminId))]
    public Admin Admin { get; set; }

    public List<Service> Services { get; set; } = new List<Service>();
}

public class Service
{
    [Key, MaxLength(10)]
    public string ServiceId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; }

    public string Description { get; set; }

    [MaxLength(10)]
    public string CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public ServiceCategory Category { get; set; }

    [MaxLength(10)]
    public string AdminId { get; set; }   // creator admin

    [ForeignKey(nameof(AdminId))]
    public Admin Admin { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? Price { get; set; }

    public int? DurationTime { get; set; } // minutes

    public List<Appointment> Appointments { get; set; } = new List<Appointment>();
}

public class Pet
{
    [Key, MaxLength(10)]
    public string PetId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; }

    public int? Age { get; set; }

    [MaxLength(100)]
    public string Type { get; set; }   // e.g., Dog, Cat

    [MaxLength(100)]
    public string Breed { get; set; }

    [MaxLength(500)]
    public string Remark { get; set; }

    // photo path (or switch to byte[] for blob)
    [MaxLength(300)]
    public string Photo { get; set; }

    // FK to customer who owns pet
    [MaxLength(10)]
    public string CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; }

    // optional admin reference
    [MaxLength(10)]
    public string AdminId { get; set; }

    [ForeignKey(nameof(AdminId))]
    public Admin Admin { get; set; }

    public List<Appointment> Appointments { get; set; } = new List<Appointment>();
}

public class RedeemGift
{
    [Key, MaxLength(10)]
    public string GiftId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; }

    public int Quantity { get; set; } = 0;

    [MaxLength(300)]
    public string Photo { get; set; }

    [MaxLength(10)]
    public string AdminId { get; set; }

    [ForeignKey(nameof(AdminId))]
    public Admin Admin { get; set; }

    public List<CustomerRedeemGift> CustomerRedeemGifts { get; set; } = new List<CustomerRedeemGift>();
}

/* =========================
   APPOINTMENT
   ========================= */
public class Appointment
{
    [Key, MaxLength(15)]
    public string AppointmentId { get; set; }

    [MaxLength(50)]
    public string Status { get; set; }        // Booked, Confirmed, Completed, Cancelled

    public int? DurationTime { get; set; }    // minutes

    public DateTime? AppointmentDateTime { get; set; }

    [MaxLength(500)]
    public string SpecialRequest { get; set; }

    // FKs
    [MaxLength(10)]
    public string CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; }

    [MaxLength(10)]
    public string PetId { get; set; }

    [ForeignKey(nameof(PetId))]
    public Pet Pet { get; set; }

    [MaxLength(10)]
    public string StaffId { get; set; }

    [ForeignKey(nameof(StaffId))]
    public Staff Staff { get; set; }

    [MaxLength(10)]
    public string AdminId { get; set; }

    [ForeignKey(nameof(AdminId))]
    public Admin Admin { get; set; }

    // appointment maps to a service
    [MaxLength(10)]
    public string ServiceId { get; set; }

    [ForeignKey(nameof(ServiceId))]
    public Service Service { get; set; }

    public DateTime CreatedAt { get; set; }
}

/* =========================
   CUSTOMER REDEEM GIFT
   ========================= */
public class CustomerRedeemGift
{
    [Key, MaxLength(15)]
    public string CrgId { get; set; }

    [MaxLength(10)]
    public string CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; }

    [MaxLength(10)]
    public string GiftId { get; set; }

    [ForeignKey(nameof(GiftId))]
    public RedeemGift Gift { get; set; }

    public DateTime? RedeemDate { get; set; }

    public int QuantityRedeemed { get; set; }
}
