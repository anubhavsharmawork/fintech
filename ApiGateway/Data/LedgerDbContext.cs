using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Data;

public class LedgerAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string AccountNumber { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "NZD";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class LedgerTransaction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NZD";
    public string Type { get; set; } = null!; // credit, debit
    public string Description { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class LedgerPayee
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public string AccountNumber { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }

    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<LedgerPayee> LedgerPayees => Set<LedgerPayee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LedgerAccount>(e =>
        {
            e.ToTable("LedgerAccounts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AccountNumber).IsUnique();
            e.Property(x => x.AccountNumber).HasMaxLength(20).IsRequired();
            e.Property(x => x.AccountType).HasMaxLength(50).IsRequired();
            e.Property(x => x.Balance).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        });

        modelBuilder.Entity<LedgerTransaction>(e =>
        {
            e.ToTable("LedgerTransactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.Type).HasMaxLength(10).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.AccountId);
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<LedgerPayee>(e =>
        {
            e.ToTable("LedgerPayees");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.AccountNumber).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.UserId, x.AccountNumber }).IsUnique();
        });
    }
}
