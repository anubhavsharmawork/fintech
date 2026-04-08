using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using AccountService.Models;
using AccountService.Services;

namespace AccountService.Data;

public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string AccountNumber { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "NZD";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsCrypto { get; set; } = false;
    public string? Blockchain { get; set; }
    public string? Address { get; set; }
    public string? TokenSymbol { get; set; } = "FTK";
    public uint RowVersion { get; set; }
    public string ClientType { get; set; } = "Individual";
    public Guid? OrganisationId { get; set; }
}

/// <summary>
/// Minimal read-only projection of the LedgerTransactions table owned by TransactionService.
/// AccountService uses this only to compute held/available balances — it never writes here.
/// ExcludeFromMigrations() ensures EF never tries to create or alter this table.
/// </summary>
public class LedgerTransactionProjection
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>credit or debit</summary>
    public string Type { get; set; } = null!;

    /// <summary>Non-null when the transaction belongs to a payment batch (potentially in-flight).</summary>
    public Guid? PaymentBatchId { get; set; }
}

/// <summary>
/// Read-only projection of LedgerEntries for computing account balances via double-entry bookkeeping.
/// </summary>
public class LedgerEntryProjection
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Guid AccountId { get; set; }
    public string EntryType { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class AccountDbContext : DbContext
{
    private readonly IEncryptionService _encryption;

    public AccountDbContext(DbContextOptions<AccountDbContext> options, IEncryptionService? encryption = null)
        : base(options)
    {
        _encryption = encryption ?? NullEncryptionService.Instance;
    }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<BankConnection> BankConnections { get; set; }
    public DbSet<ExternalBankAccount> ExternalBankAccounts { get; set; }
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

    /// <summary>
    /// Read-only projection of LedgerTransactions owned by TransactionService.
    /// Used only to compute held/available balances — never written to from here.
    /// </summary>
    public DbSet<LedgerTransactionProjection> LedgerTransactions { get; set; }

    /// <summary>
    /// Read-only projection of LedgerEntries for double-entry balance computation.
    /// </summary>
    public DbSet<LedgerEntryProjection> LedgerEntries { get; set; }

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
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.Property(e => e.ClientType).HasMaxLength(20).HasDefaultValue("Individual").IsRequired();
            entity.HasIndex(e => e.OrganisationId);
        });

        modelBuilder.Entity<LedgerTransactionProjection>(entity =>
        {
            entity.ToTable("LedgerTransactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Type).HasMaxLength(10).IsRequired();
            entity.ToTable(t => t.ExcludeFromMigrations());
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
            entity.Property(e => e.AccessToken).HasMaxLength(500).IsRequired(false)
                .HasConversion(new ValueConverter<string?, string?>(
                    v => v != null ? _encryption.Encrypt(v) : null,
                    v => v != null ? _encryption.Decrypt(v) : null));
        });

        modelBuilder.Entity<ExternalBankAccount>(entity =>
        {
            entity.ToTable("ExternalBankAccounts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalAccountId).IsUnique();
            entity.Property(e => e.ExternalAccountId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AccountName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AccountType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AccountNumber).HasMaxLength(500).IsRequired()
                .HasConversion(new ValueConverter<string, string>(
                    v => _encryption.Encrypt(v),
                    v => _encryption.Decrypt(v)));
            entity.Property(e => e.Balance).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.HasOne(e => e.BankConnection)
                .WithMany()
                .HasForeignKey(e => e.BankConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<LedgerEntryProjection>(entity =>
        {
            entity.ToTable("LedgerEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntryType).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.HasIndex(e => e.AccountId);
            entity.ToTable(t => t.ExcludeFromMigrations());
        });
    }

    /// <summary>No-op encryption used in unit tests where no key is configured.</summary>
    private sealed class NullEncryptionService : IEncryptionService
    {
        public static readonly NullEncryptionService Instance = new();
        public string Encrypt(string plaintext) => plaintext;
        public string Decrypt(string ciphertext) => ciphertext;
    }
}
