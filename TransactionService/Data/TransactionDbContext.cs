using Microsoft.EntityFrameworkCore;
using TransactionService.Models;

namespace TransactionService.Data;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NZD";
    public string Type { get; set; } = null!; // credit, debit
    public string Description { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string? SpendingType { get; set; }
    public string? TxHash { get; set; }
    public string ClientType { get; set; } = "Individual";
    public Guid? OrganisationId { get; set; }
    public Guid? PaymentBatchId { get; set; }
    public string Status { get; set; } = "Completed"; // Completed, Pending, Failed, Processing
}

public class TransactionDbContext : DbContext
{
    public TransactionDbContext(DbContextOptions<TransactionDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<SuspiciousActivityReport> SuspiciousActivityReports { get; set; }
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }
    public DbSet<LedgerEntry> LedgerEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("LedgerTransactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.SpendingType).HasMaxLength(20).IsRequired(false);
            entity.Property(e => e.TxHash).HasMaxLength(100).IsRequired(false);
            entity.Property(e => e.ClientType).HasMaxLength(20).HasDefaultValue("Individual").IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Completed").IsRequired();
            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.SpendingType);
            entity.HasIndex(e => e.OrganisationId);
            entity.HasIndex(e => e.PaymentBatchId);
        });

        modelBuilder.Entity<SuspiciousActivityReport>(entity =>
        {
            entity.ToTable("SuspiciousActivityReports");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.Property(e => e.RiskLevel).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000).IsRequired(false);
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(64).IsRequired();
            entity.Property(e => e.RequestPath).HasMaxLength(500).IsRequired();
            entity.Property(e => e.RequestMethod).HasMaxLength(10).IsRequired();
            entity.Property(e => e.ResponseBody).IsRequired();
            entity.Property(e => e.IsProcessing).HasDefaultValue(false);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);
        });

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.ToTable("LedgerEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntryType).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.AccountId);
        });
    }
}
