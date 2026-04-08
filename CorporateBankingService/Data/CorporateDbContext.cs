using Microsoft.EntityFrameworkCore;
using CorporateBankingService.Models;

namespace CorporateBankingService.Data;

public class CorporateDbContext : DbContext
{
    public CorporateDbContext(DbContextOptions<CorporateDbContext> options) : base(options) { }

    public DbSet<Organisation> Organisations { get; set; }
    public DbSet<OrganisationMember> OrganisationMembers { get; set; }
    public DbSet<ApprovalPolicy> ApprovalPolicies { get; set; }
    public DbSet<PaymentBatch> PaymentBatches { get; set; }
    public DbSet<PaymentBatchItem> PaymentBatchItems { get; set; }
    public DbSet<ApprovalRecord> ApprovalRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organisation>(entity =>
        {
            entity.ToTable("corp_organisations");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RegistrationNumber).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.RegistrationNumber).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<OrganisationMember>(entity =>
        {
            entity.ToTable("corp_organisation_members");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrganisationId, e.UserId }).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(254).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.HasOne(e => e.Organisation)
                .WithMany()
                .HasForeignKey(e => e.OrganisationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApprovalPolicy>(entity =>
        {
            entity.ToTable("corp_approval_policies");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrganisationId);
            entity.Property(e => e.MonetaryThreshold).HasPrecision(18, 2);
            entity.HasOne(e => e.Organisation)
                .WithMany()
                .HasForeignKey(e => e.OrganisationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaymentBatch>(entity =>
        {
            entity.ToTable("corp_payment_batches");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrganisationId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.HasOne(e => e.Organisation)
                .WithMany()
                .HasForeignKey(e => e.OrganisationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaymentBatchItem>(entity =>
        {
            entity.ToTable("corp_payment_batch_items");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PaymentBatchId);
            entity.Property(e => e.PayeeName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PayeeAccountNumber).HasMaxLength(50);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasOne(e => e.PaymentBatch)
                .WithMany(b => b.Items)
                .HasForeignKey(e => e.PaymentBatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApprovalRecord>(entity =>
        {
            entity.ToTable("corp_approval_records");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PaymentBatchId);
            entity.Property(e => e.Decision).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Comments).HasMaxLength(500);
            entity.HasOne(e => e.PaymentBatch)
                .WithMany(b => b.Approvals)
                .HasForeignKey(e => e.PaymentBatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
