using Microsoft.EntityFrameworkCore;

namespace UserService.Data;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public bool IsEmailVerified { get; set; }
    public string KycStatus { get; set; } = "Pending";
    public string ClientType { get; set; } = "Individual";
    public Guid? OrganisationId { get; set; }
    public string? OrganisationRole { get; set; }
    public string? CompanyName { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? TimeZoneId { get; set; }
    public int? UtcOffsetMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            // Use a non-conflicting table name in the shared database
            entity.ToTable("users_usvc");

            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(254).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.KycStatus).HasMaxLength(20).HasDefaultValue("Pending").IsRequired();
            entity.Property(e => e.ClientType).HasMaxLength(20).HasDefaultValue("Individual").IsRequired();
            entity.Property(e => e.OrganisationRole).HasMaxLength(20).IsRequired(false);
            entity.Property(e => e.CompanyName).HasMaxLength(200).IsRequired(false);
            entity.Property(e => e.RegistrationNumber).HasMaxLength(50).IsRequired(false);
            entity.Property(e => e.TimeZoneId).HasMaxLength(100).IsRequired(false);
            entity.Property(e => e.UtcOffsetMinutes).IsRequired(false);
            entity.HasIndex(e => e.OrganisationId);
        });
    }
}