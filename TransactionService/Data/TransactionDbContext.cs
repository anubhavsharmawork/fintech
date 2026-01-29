using Microsoft.EntityFrameworkCore;

namespace TransactionService.Data;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Type { get; set; } = null!; // credit, debit
    public string Description { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string? SpendingType { get; set; }
    public string? TxHash { get; set; }
}

public class TransactionDbContext : DbContext
{
    public TransactionDbContext(DbContextOptions<TransactionDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions { get; set; }

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
            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.SpendingType);
        });
    }
}