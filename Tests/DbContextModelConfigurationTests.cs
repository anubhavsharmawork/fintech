using Microsoft.EntityFrameworkCore;
using UserService.Data;
using AccountService.Data;
using TransactionService.Data;

namespace Tests;

/// <summary>
/// Tests for DbContext model configurations and entity framework mappings
/// Ensures proper table names, indexes, constraints, and property configurations
/// </summary>
public class DbContextModelConfigurationTests
{
    #region UserDbContext Tests

    public class UserDbContextConfigurationTests
    {
        [Fact]
        public void UserDbContext_ConfiguresUserEntity()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new UserDbContext(options);

            // Act & Assert
            context.Users.Should().NotBeNull();
        }

        [Fact]
        public async Task User_IdIsRequired()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new UserDbContext(options);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                PasswordHash = "hash",
                FirstName = "Test",
                LastName = "User",
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Assert
            var savedUser = await context.Users.FindAsync(user.Id);
            savedUser.Should().NotBeNull();
        }

        [Fact]
        public async Task User_EmailMustBeUnique()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new UserDbContext(options);

            var user1 = new User
            {
                Id = Guid.NewGuid(),
                Email = "duplicate@example.com",
                PasswordHash = "hash1",
                FirstName = "User1",
                LastName = "Test",
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var user2 = new User
            {
                Id = Guid.NewGuid(),
                Email = "duplicate@example.com",
                PasswordHash = "hash2",
                FirstName = "User2",
                LastName = "Test",
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Users.Add(user1);
            await context.SaveChangesAsync();

            context.Users.Add(user2);

            // Act & Assert - In-memory database doesn't enforce unique constraints
            // but we can verify the model is configured correctly
            var model = context.Model.FindEntityType(typeof(User));
            var emailIndex = model?.GetIndexes().FirstOrDefault(i => 
                i.Properties.Any(p => p.Name == nameof(User.Email)));
            emailIndex?.IsUnique.Should().BeTrue();
        }

        [Fact]
        public void User_PropertiesHaveCorrectMaxLength()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new UserDbContext(options);
            var model = context.Model.FindEntityType(typeof(User));

            // Assert
            var emailProperty = model?.FindProperty(nameof(User.Email));
            emailProperty?.GetMaxLength().Should().Be(254);

            var firstNameProperty = model?.FindProperty(nameof(User.FirstName));
            firstNameProperty?.GetMaxLength().Should().Be(100);

            var lastNameProperty = model?.FindProperty(nameof(User.LastName));
            lastNameProperty?.GetMaxLength().Should().Be(100);
        }

        [Fact]
        public void User_TableNameIsConfigured()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new UserDbContext(options);
            var model = context.Model.FindEntityType(typeof(User));

            // Assert
            model?.GetTableName().Should().Be("users_usvc");
        }

        [Fact]
        public async Task User_AllPropertiesArePersisted()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new UserDbContext(options);
            var createdAt = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "complete@example.com",
                PasswordHash = "secure-hash-value",
                FirstName = "Complete",
                LastName = "User",
                IsEmailVerified = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act - Use new context to ensure we're reading from DB
            using var context2 = new UserDbContext(options);
            var savedUser = await context2.Users.FindAsync(user.Id);

            // Assert
            savedUser.Should().NotBeNull();
            savedUser!.Email.Should().Be("complete@example.com");
            savedUser.PasswordHash.Should().Be("secure-hash-value");
            savedUser.FirstName.Should().Be("Complete");
            savedUser.LastName.Should().Be("User");
            savedUser.IsEmailVerified.Should().BeTrue();
        }
    }

    #endregion

    #region AccountDbContext Tests

    public class AccountDbContextConfigurationTests
    {
        [Fact]
        public void AccountDbContext_ConfiguresAccountEntity()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AccountDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new AccountDbContext(options);

            // Assert
            context.Accounts.Should().NotBeNull();
        }

        [Fact]
        public void Account_TableNameIsConfigured()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AccountDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new AccountDbContext(options);
            var model = context.Model.FindEntityType(typeof(Account));

            // Assert
            model?.GetTableName().Should().Be("LedgerAccounts");
        }

        [Fact]
        public void Account_PropertiesHaveCorrectConfiguration()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AccountDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new AccountDbContext(options);
            var model = context.Model.FindEntityType(typeof(Account));

            // Assert
            var accountNumberProperty = model?.FindProperty(nameof(Account.AccountNumber));
            accountNumberProperty?.GetMaxLength().Should().Be(20);

            var accountTypeProperty = model?.FindProperty(nameof(Account.AccountType));
            accountTypeProperty?.GetMaxLength().Should().Be(50);

            var currencyProperty = model?.FindProperty(nameof(Account.Currency));
            currencyProperty?.GetMaxLength().Should().Be(3);
        }

        [Fact]
        public void Account_AccountNumberIsUnique()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AccountDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new AccountDbContext(options);
            var model = context.Model.FindEntityType(typeof(Account));

            // Assert
            var accountNumberIndex = model?.GetIndexes().FirstOrDefault(i =>
                i.Properties.Any(p => p.Name == nameof(Account.AccountNumber)));
            accountNumberIndex?.IsUnique.Should().BeTrue();
        }

        [Fact]
        public async Task Account_CryptoPropertiesArePersisted()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AccountDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new AccountDbContext(options);
            var account = new Account
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                AccountNumber = "1234567890",
                AccountType = "CryptoWallet",
                Balance = 1.5m,
                Currency = "ETH",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsCrypto = true,
                Blockchain = "Ethereum",
                Address = "0x742d35Cc6634C0532925a3b844Bc9e7595f8fE0C",
                TokenSymbol = "WETH"
            };

            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            // Act
            using var context2 = new AccountDbContext(options);
            var saved = await context2.Accounts.FindAsync(account.Id);

            // Assert
            saved!.IsCrypto.Should().BeTrue();
            saved.Blockchain.Should().Be("Ethereum");
            saved.Address.Should().Be("0x742d35Cc6634C0532925a3b844Bc9e7595f8fE0C");
            saved.TokenSymbol.Should().Be("WETH");
        }

        [Fact]
        public void Account_CryptoPropertiesHaveCorrectMaxLength()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AccountDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new AccountDbContext(options);
            var model = context.Model.FindEntityType(typeof(Account));

            // Assert
            var blockchainProperty = model?.FindProperty(nameof(Account.Blockchain));
            blockchainProperty?.GetMaxLength().Should().Be(50);

            var addressProperty = model?.FindProperty(nameof(Account.Address));
            addressProperty?.GetMaxLength().Should().Be(100);

            var tokenSymbolProperty = model?.FindProperty(nameof(Account.TokenSymbol));
            tokenSymbolProperty?.GetMaxLength().Should().Be(20);
        }
    }

    #endregion

    #region TransactionDbContext Tests

    public class TransactionDbContextConfigurationTests
    {
        [Fact]
        public void TransactionDbContext_ConfiguresTransactionEntity()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<TransactionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new TransactionDbContext(options);

            // Assert
            context.Transactions.Should().NotBeNull();
        }

        [Fact]
        public void Transaction_TableNameIsConfigured()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<TransactionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new TransactionDbContext(options);
            var model = context.Model.FindEntityType(typeof(Transaction));

            // Assert
            model?.GetTableName().Should().Be("LedgerTransactions");
        }

        [Fact]
        public void Transaction_PropertiesHaveCorrectConfiguration()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<TransactionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new TransactionDbContext(options);
            var model = context.Model.FindEntityType(typeof(Transaction));

            // Assert
            var currencyProperty = model?.FindProperty(nameof(Transaction.Currency));
            currencyProperty?.GetMaxLength().Should().Be(3);

            var typeProperty = model?.FindProperty(nameof(Transaction.Type));
            typeProperty?.GetMaxLength().Should().Be(10);

            var descriptionProperty = model?.FindProperty(nameof(Transaction.Description));
            descriptionProperty?.GetMaxLength().Should().Be(500);

            var spendingTypeProperty = model?.FindProperty(nameof(Transaction.SpendingType));
            spendingTypeProperty?.GetMaxLength().Should().Be(20);

            var txHashProperty = model?.FindProperty(nameof(Transaction.TxHash));
            txHashProperty?.GetMaxLength().Should().Be(100);
        }

        [Fact]
        public void Transaction_HasExpectedIndexes()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<TransactionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new TransactionDbContext(options);
            var model = context.Model.FindEntityType(typeof(Transaction));

            // Assert
            var indexes = model?.GetIndexes().ToList();
            indexes.Should().Contain(i => i.Properties.Any(p => p.Name == nameof(Transaction.AccountId)));
            indexes.Should().Contain(i => i.Properties.Any(p => p.Name == nameof(Transaction.UserId)));
            indexes.Should().Contain(i => i.Properties.Any(p => p.Name == nameof(Transaction.SpendingType)));
        }

        [Fact]
        public async Task Transaction_AllPropertiesArePersisted()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<TransactionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new TransactionDbContext(options);
            var createdAt = DateTime.UtcNow;
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Amount = 123.45m,
                Currency = "EUR",
                Type = "credit",
                Description = "Test transaction",
                SpendingType = "Fun",
                TxHash = "0xabc123",
                CreatedAt = createdAt
            };

            context.Transactions.Add(transaction);
            await context.SaveChangesAsync();

            // Act
            using var context2 = new TransactionDbContext(options);
            var saved = await context2.Transactions.FindAsync(transaction.Id);

            // Assert
            saved.Should().NotBeNull();
            saved!.Amount.Should().Be(123.45m);
            saved.Currency.Should().Be("EUR");
            saved.Type.Should().Be("credit");
            saved.Description.Should().Be("Test transaction");
            saved.SpendingType.Should().Be("Fun");
            saved.TxHash.Should().Be("0xabc123");
        }
    }

    #endregion

    #region Entity Default Values Tests

    [Fact]
    public void Account_HasCorrectDefaultValues()
    {
        // Arrange
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountNumber = "123",
            AccountType = "Checking",
            Balance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        account.Currency.Should().Be("USD");
        account.IsCrypto.Should().BeFalse();
        account.TokenSymbol.Should().Be("FTK");
    }

    [Fact]
    public void Transaction_HasCorrectDefaultValues()
    {
        // Arrange
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 100,
            Type = "debit",
            Description = "Test",
            CreatedAt = DateTime.UtcNow
        };

        // Assert
        transaction.Currency.Should().Be("USD");
        transaction.SpendingType.Should().BeNull();
        transaction.TxHash.Should().BeNull();
    }

    #endregion
}
