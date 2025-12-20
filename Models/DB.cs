using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetGroomingAppointmentSystem.Models;

#nullable disable

public class DB : DbContext
{
    public DB(DbContextOptions<DB> options) : base(options) { }

    // DbSets
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Staff> Staffs { get; set; }
    public DbSet<Customer> Customers { get; set; }

    public DbSet<ServiceCategory> ServiceCategories { get; set; }
    public DbSet<Service> Services { get; set; }

    public DbSet<ServiceServiceCategory> ServiceServiceCategories { get; set; }

    public DbSet<Pet> Pets { get; set; }
    public DbSet<RedeemGift> RedeemGifts { get; set; }

    public DbSet<Appointment> Appointments { get; set; }

    public DbSet<CustomerRedeemGift> CustomerRedeemGifts { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ServiceServiceCategory>()
            .HasOne(ssc => ssc.Service)
            .WithMany(s => s.ServiceServiceCategories)
            .HasForeignKey(ssc => ssc.ServiceId);

        modelBuilder.Entity<ServiceServiceCategory>()
            .HasOne(ssc => ssc.Category)
            .WithMany(c => c.ServiceServiceCategories)
            .HasForeignKey(ssc => ssc.CategoryId);
    }
}

/* =========================
 USER BASE CLASS
 ========================= */
public class User
{
    [Key, MaxLength(10)]
    public string UserId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; }

    [MaxLength(20)]
    public string IC { get; set; }

    [MaxLength(150)]
    [EmailAddress]
    public string Email { get; set; }

    [MaxLength(20)]
    public string Phone { get; set; }

    [Required, MaxLength(200)]
    public string Password { get; set; }

    // Allow storing multiple photo URLs (comma-separated) or long CloudFront URLs
    [Column(TypeName = "nvarchar(max)")]
    public string Photo { get; set; }

    [Required, MaxLength(20)]
    public string Role { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual List<Pet> Pets { get; set; } = new List<Pet>();
    public virtual List<Appointment> Appointments { get; set; } = new List<Appointment>();
}

/* =========================
 CHILD CLASSES
 ========================= */
[Table("Admins")]
public class Admin : User
{
    public virtual List<ServiceCategory> ServiceCategories { get; set; } = new List<ServiceCategory>();
    public virtual List<Service> Services { get; set; } = new List<Service>();
    public virtual List<RedeemGift> RedeemGifts { get; set; } = new List<RedeemGift>();
    [NotMapped]
    public virtual List<Pet> ManagedPets { get; set; } = new List<Pet>();
    [NotMapped]
    public virtual List<Staff> ManagedStaff { get; set; } = new List<Staff>();
}

[Table("Staffs")]
public class Staff : User
{
    [MaxLength(500)]
    public string Description { get; set; }

    public int? ExperienceYear { get; set; }

    [MaxLength(100)]
    public string Position { get; set; }

    [MaxLength(10)]
    public string? AdminUserId { get; set; }

    [ForeignKey(nameof(AdminUserId))]
    public virtual Admin? Admin { get; set; }
}

[Table("Customers")]
public class Customer : User
{
    public int LoyaltyPoint { get; set; } = 0;

    [MaxLength(50)]
    public string Status { get; set; }

    public DateTime? RegisteredDate { get; set; }

    public virtual List<CustomerRedeemGift> Redeems { get; set; } = new List<CustomerRedeemGift>();
}

/* =========================
 PET & SERVICE CATEGORY
 ========================= */
public class Pet
{
    [Key, MaxLength(10)]
    public string PetId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; }

    public int? Age { get; set; }

    [MaxLength(100)]
    public string Type { get; set; }

    [MaxLength(100)]
    public string Breed { get; set; }

    [MaxLength(500)]
    public string Remark { get; set; }

    [MaxLength(300)]
    public string Photo { get; set; }

    [MaxLength(10)]
    public string CustomerId { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public virtual Customer Customer { get; set; }

    [MaxLength(10)]
    public string? AdminId { get; set; }

    [ForeignKey(nameof(AdminId))]
    public virtual Admin? Admin { get; set; }

    public virtual List<Appointment> Appointments { get; set; } = new List<Appointment>();
}

public class ServiceCategory
{
    [Key, MaxLength(10)]
    public string CategoryId { get; set; }

    [MaxLength(20)]
    public string PetType { get; set; }

    [MaxLength(10)]
    public string AdminId { get; set; }

    [ForeignKey(nameof(AdminId))]
    public virtual Admin Admin { get; set; }

    public virtual List<ServiceServiceCategory> ServiceServiceCategories { get; set; } = new List<ServiceServiceCategory>();
}

/* =========================
 SERVICE
 ========================= */
public class Service
{
    [Key, MaxLength(10)]
    public string ServiceId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; }

    [MaxLength(500)]
    public string Description { get; set; }

    [MaxLength(10)]
    public string AdminId { get; set; }

    [ForeignKey(nameof(AdminId))]
    public virtual Admin Admin { get; set; }

    public int? DurationTime { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    public virtual List<Appointment> Appointments { get; set; } = new List<Appointment>();
    public virtual List<ServiceServiceCategory> ServiceServiceCategories { get; set; } = new List<ServiceServiceCategory>();
}

/* =========================
 APPOINTMENT
 ========================= */
public class Appointment
{
    [Key]
    public string AppointmentId { get; set; }

    [MaxLength(50)]
    public string Status { get; set; }

    public int? DurationTime { get; set; }

    public DateTime? AppointmentDateTime { get; set; }

    [MaxLength(500)]
    public string SpecialRequest { get; set; }

    [MaxLength(10)]
    public string CustomerId { get; set; }
    [ForeignKey(nameof(CustomerId))]
    public virtual Customer Customer { get; set; }

    [MaxLength(10)]
    public string PetId { get; set; }
    [ForeignKey(nameof(PetId))]
    public virtual Pet Pet { get; set; }

    [MaxLength(10)]
    public string StaffId { get; set; }
    [ForeignKey(nameof(StaffId))]
    public virtual Staff Staff { get; set; }

    [MaxLength(10)]
    public string AdminId { get; set; }
    [ForeignKey(nameof(AdminId))]
    public virtual Admin Admin { get; set; }

    [MaxLength(10)]
    public string ServiceId { get; set; }
    [ForeignKey(nameof(ServiceId))]
    public virtual Service Service { get; set; }

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
    public virtual Customer Customer { get; set; }

    [MaxLength(10)]
    public string GiftId { get; set; }
    [ForeignKey(nameof(GiftId))]
    public virtual RedeemGift Gift { get; set; }

    public DateTime? RedeemDate { get; set; }
    public int QuantityRedeemed { get; set; }
}

/* =========================
 REDEEM GIFT
 ========================= */
public class RedeemGift
{
    [Key, MaxLength(10)]
    public string GiftId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; }

    public int Quantity { get; set; } = 0;
    public int LoyaltyPointCost { get; set; }

    public bool IsDeleted { get; set; } = false;   // ✅ ADD THIS

    [MaxLength(300)]
    public string Photo { get; set; }

    [MaxLength(10)]
    public string AdminId { get; set; }
    public virtual Admin Admin { get; set; }

    public virtual List<CustomerRedeemGift> CustomerRedeemGifts { get; set; }
}


/* =========================
 JUNCTION TABLE
 ========================= */
public class ServiceServiceCategory
{
    [Key, MaxLength(6)]
    public string SscId { get; set; }

    [MaxLength(10)]
    public string ServiceId { get; set; }
    [ForeignKey(nameof(ServiceId))]
    public virtual Service Service { get; set; }

    [MaxLength(10)]
    public string CategoryId { get; set; }
    [ForeignKey(nameof(CategoryId))]
    public virtual ServiceCategory Category { get; set; }
}

/* =========================
 PASSWORD RESET
 ========================= */
public class PasswordResetToken
{
    [Key]
    public int Id { get; set; }

    [MaxLength(10)]
    public string CustomerId { get; set; }
    [ForeignKey(nameof(CustomerId))]
    public virtual Customer Customer { get; set; }

    [MaxLength(150)]
    public string Email { get; set; }

    [MaxLength(20)]
    public string Phone { get; set; }

    [MaxLength(6)]
    public string VerificationCode { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsVerified { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? VerifiedAt { get; set; }
}
