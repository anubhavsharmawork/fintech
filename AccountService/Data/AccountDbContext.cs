using Microsoft.EntityFrameworkCore;
using AccountService.Models;

namespace AccountService.Data;

public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string AccountNumber { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsCrypto { get; set; } = false;
    public string? Blockchain { get; set; }
    public string? Address { get; set; }
    public string? TokenSymbol { get; set; } = "FTK";
}

public class AccountDbContext : DbContext
{
    public AccountDbContext(DbContextOptions<AccountDbContext> options) : base(options) { }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<BankConnection> BankConnections { get; set; }
    public DbSet<ExternalBankAccount> ExternalBankAccounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("LedgerAccounts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AccountNumber).IsUnique();
            entity.Property(e => e.AccountNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AccountType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Balance).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Blockchain).HasMaxLength(50).IsRequired(false);
            entity.Property(e => e.Address).HasMaxLength(100).IsRequired(false);
            entity.Property(e => e.TokenSymbol).HasMaxLength(20).IsRequired(false);
        });

        modelBuilder.Entity<BankConnection>(entity =>
        {
            entity.ToTable("BankConnections");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.BankId }).IsUnique();
            entity.Property(e => e.BankId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.BankName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.BankLogo).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AccessToken).HasMaxLength(500).IsRequired(false);
        });

        modelBuilder.Entity<ExternalBankAccount>(entity =>
        {
            entity.ToTable("ExternalBankAccounts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalAccountId).IsUnique();
            entity.Property(e => e.ExternalAccountId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AccountName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AccountType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AccountNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Balance).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.HasOne(e => e.BankConnection)
                .WithMany()
                .HasForeignKey(e => e.BankConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}