using ApiGateway.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Data;

public class SanctionDbContext : DbContext
{
    public SanctionDbContext(DbContextOptions<SanctionDbContext> options) : base(options) { }

    public DbSet<SanctionRequest> SanctionRequests => Set<SanctionRequest>();
    public DbSet<SanctionAuditLog> SanctionAuditLogs => Set<SanctionAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SanctionRequest>(entity =>
        {
            entity.ToTable("SanctionRequests");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => new { e.ExternalProjectId, e.UserId });
            entity.HasIndex(e => e.Status);

            entity.Property(e => e.ExternalProjectId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ExternalTenantId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.RequestedAmount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Purpose).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.ApprovedAmount).HasPrecision(18, 2);
            entity.Property(e => e.DecisionReason).HasMaxLength(2000);
            entity.Property(e => e.FtkTransactionRef).HasMaxLength(200);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(200).IsRequired();

            entity.Property(e => e.KycStatus)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.AmlStatus)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20);
        });

        modelBuilder.Entity<SanctionAuditLog>(entity =>
        {
            entity.ToTable("SanctionAuditLogs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SanctionRequestId);

            entity.Property(e => e.ChangedBy).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(500).IsRequired();

            entity.Property(e => e.FromStatus)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.ToStatus)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasOne(e => e.SanctionRequest)
                .WithMany(s => s.AuditLogs)
                .HasForeignKey(e => e.SanctionRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
