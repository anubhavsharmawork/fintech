using ApiGateway.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Data;

public class CreditDbContext : DbContext
{
    public CreditDbContext(DbContextOptions<CreditDbContext> options) : base(options) { }

    public DbSet<CreditFacility> CreditFacilities => Set<CreditFacility>();
    public DbSet<CreditRepayment> CreditRepayments => Set<CreditRepayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CreditFacility>(entity =>
        {
            entity.ToTable("CreditFacilities");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.WalletAddress);

            entity.Property(e => e.WalletAddress).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CreditLimit).HasPrecision(18, 2);
            entity.Property(e => e.DrawnAmount).HasPrecision(18, 2);
            entity.Property(e => e.OutstandingBalance).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(10).IsRequired();

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20);
        });

        modelBuilder.Entity<CreditRepayment>(entity =>
        {
            entity.ToTable("CreditRepayments");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FacilityId);
            entity.HasIndex(e => e.UserId);

            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();

            entity.HasOne(e => e.Facility)
                .WithMany(f => f.Repayments)
                .HasForeignKey(e => e.FacilityId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
