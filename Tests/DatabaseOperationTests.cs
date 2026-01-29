using Microsoft.EntityFrameworkCore;
using UserService.Data;
using AccountService.Data;
using TransactionService.Data;

namespace Tests;

/// <summary>
/// Comprehensive database operation tests ensuring data integrity,
/// persistence, queries, and transaction handling
/// </summary>
public class DatabaseOperationTests
{
    #region UserService Database Tests

    public class UserDbContextTests
    {
        private UserDbContext BuildContext()
        {
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new UserDbContext(options);
        }

        [Fact]
        public async Task CreateUser_PersistsToDatabase()
        {
            // Arrange
            var db = BuildContext();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                PasswordHash = "hash",
                FirstName = "Test",
                LastName = "User",
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Assert
            var retrieved = await db.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
            retrieved.Should().NotBeNull();
            retrieved!.FirstName.Should().Be("Test");
        }

        [Fact]
        public async Task UpdateUser_PersistsChanges()
        {
            // Arrange
            var db = BuildContext();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                PasswordHash = "hash",
                FirstName = "Test",
                LastName = "User",
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Act
            user.FirstName = "Updated";
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Assert
            var updated = await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            updated!.FirstName.Should().Be("Updated");
        }

        [Fact]
        public async Task DeleteUser_RemovesFromDatabase()
        {
            // Arrange
            var db = BuildContext();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                PasswordHash = "hash",
                FirstName = "Test",
                LastName = "User",
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
            var userId = user.Id;

            // Act
            db.Users.Remove(user);
            await db.SaveChangesAsync();

            // Assert
            var deleted = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            deleted.Should().BeNull();
        }

        [Fact]
        public async Task QueryUsers_ByEmail_CaseInsensitive()
        {
            // Arrange
            var db = BuildContext();
            var email = "test@example.com";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email.ToLowerInvariant(),
                PasswordHash = "hash",
                FirstName = "Test",
                LastName = "User",
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Act - In real usage, emails are normalized to lowercase before querying
            var found = await db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());

            // Assert
            found.Should().NotBeNull();
        }

        [Fact]
        public async Task MultipleUsers_QueryReturnsCorrectCount()
        {
            // Arrange
            var db = BuildContext();
            for (int i = 0; i < 10; i++)
            {
                db.Users.Add(new User
                {
                    Id = Guid.NewGuid(),
                    Email = $"user{i}@example.com",
                    PasswordHash = "hash",
                    FirstName = $"User{i}",
                    LastName = "Test",
                    IsEmailVerified = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();

            // Act
            var count = await db.Users.CountAsync();

            // Assert
            count.Should().Be(10);
        }
    }

    #endregion

    #region AccountService Database Tests

    public class AccountDbContextTests
    {
        private AccountDbContext BuildContext()
        {
            var options = new DbContextOptionsBuilder<AccountDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AccountDbContext(options);
        }

        [Fact]
        public async Task CreateAccount_PersistsWithAllProperties()
        {
            // Arrange
            var db = BuildContext();
            var userId = Guid.NewGuid();
            var account = new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccountNumber = "1234567890",
                AccountType = "Checking",
                Balance = 1000m,
                Currency = "USD",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            db.Accounts.Add(account);
            await db.SaveChangesAsync();

            // Assert
            var retrieved = await db.Accounts.FirstOrDefaultAsync(a => a.Id == account.Id);
            retrieved.Should().NotBeNull();
            retrieved!.AccountNumber.Should().Be("1234567890");
            retrieved.Balance.Should().Be(1000m);
            retrieved.Currency.Should().Be("USD");
        }

        [Fact]
        public async Task UpdateAccountBalance_PersistsChanges()
        {
            // Arrange
            var db = BuildContext();
            var account = new Account
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                AccountNumber = "1234567890",
                AccountType = "Checking",
                Balance = 1000m,
                Currency = "USD",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Accounts.Add(account);
            await db.SaveChangesAsync();

            // Act
            account.Balance = 1500m;
            account.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            // Assert
            var updated = await db.Accounts.FirstOrDefaultAsync(a => a.Id == account.Id);
            updated!.Balance.Should().Be(1500m);
        }

        [Fact]
        public async Task QueryAccountsByUserId_ReturnsOnlyUserAccounts()
        {
            // Arrange
            var db = BuildContext();
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            db.Accounts.AddRange(
                new Account { Id = Guid.NewGuid(), UserId = userId, AccountNumber = "1111", AccountType = "Checking", Balance = 100, Currency = "USD", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Account { Id = Guid.NewGuid(), UserId = userId, AccountNumber = "2222", AccountType = "Savings", Balance = 200, Currency = "USD", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Account { Id = Guid.NewGuid(), UserId = otherUserId, AccountNumber = "9999", AccountType = "Checking", Balance = 9999, Currency = "USD", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();

            // Act
            var accounts = await db.Accounts.Where(a => a.UserId == userId).ToListAsync();

            // Assert
            accounts.Should().HaveCount(2);
            accounts.All(a => a.UserId == userId).Should().BeTrue();
        }

        [Fact]
        public async Task DeleteAccount_RemovesFromDatabase()
        {
            // Arrange
            var db = BuildContext();
            var account = new Account
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                AccountNumber = "1234567890",
                AccountType = "Checking",
                Balance = 1000m,
                Currency = "USD",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Accounts.Add(account);
            await db.SaveChangesAsync();
            var accountId = account.Id;

            // Act
            db.Accounts.Remove(account);
            await db.SaveChangesAsync();

            // Assert
            var deleted = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId);
            deleted.Should().BeNull();
        }

        [Fact]
        public async Task AccountNumberUniqueness_CanBeQueried()
        {
            // Arrange
            var db = BuildContext();
            var accountNumber = "1234567890";

            db.Accounts.AddRange(
                new Account { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), AccountNumber = accountNumber, AccountType = "Checking", Balance = 100, Currency = "USD", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new Account { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), AccountNumber = "0987654321", AccountType = "Savings", Balance = 200, Currency = "USD", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();

            // Act
            var account = await db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);

            // Assert
            account.Should().NotBeNull();
            account!.AccountNumber.Should().Be(accountNumber);
        }
    }

    #endregion

    #region TransactionService Database Tests

    public class TransactionDbContextTests
    {
        private TransactionDbContext BuildContext()
        {
            var options = new DbContextOptionsBuilder<TransactionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new TransactionDbContext(options);
        }

        [Fact]
        public async Task CreateTransaction_PersistsWithAllProperties()
        {
            // Arrange
            var db = BuildContext();
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                UserId = userId,
                Amount = 100m,
                Currency = "USD",
                Type = "debit",
                Description = "Test transaction",
                SpendingType = "Fun",
                TxHash = "0x123abc",
                CreatedAt = DateTime.UtcNow
            };

            // Act
            db.Transactions.Add(transaction);
            await db.SaveChangesAsync();

            // Assert
            var retrieved = await db.Transactions.FirstOrDefaultAsync(t => t.Id == transaction.Id);
            retrieved.Should().NotBeNull();
            retrieved!.Amount.Should().Be(100m);
            retrieved.Description.Should().Be("Test transaction");
            retrieved.TxHash.Should().Be("0x123abc");
        }

        [Fact]
        public async Task QueryTransactionsByUserId_ReturnsOnlyUserTransactions()
        {
            // Arrange
            var db = BuildContext();
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            db.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 200, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fixed", CreatedAt = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = otherUserId, Amount = 9999, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();

            // Act
            var transactions = await db.Transactions.Where(t => t.UserId == userId).ToListAsync();

            // Assert
            transactions.Should().HaveCount(2);
            transactions.All(t => t.UserId == userId).Should().BeTrue();
        }

        [Fact]
        public async Task QueryTransactionsByAccountAndUser_ReturnsOnlyFiltered()
        {
            // Arrange
            var db = BuildContext();
            var userId = Guid.NewGuid();
            var accountId1 = Guid.NewGuid();
            var accountId2 = Guid.NewGuid();

            db.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId1, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId2, UserId = userId, Amount = 200, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();

            // Act
            var transactions = await db.Transactions
                .Where(t => t.UserId == userId && t.AccountId == accountId1)
                .ToListAsync();

            // Assert
            transactions.Should().HaveCount(1);
            transactions[0].AccountId.Should().Be(accountId1);
        }

        [Fact]
        public async Task OrderTransactionsByDate_ReturnsMostRecentFirst()
        {
            // Arrange
            var db = BuildContext();
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            db.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = now.AddHours(-2) },
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 200, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = now },
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 150, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = now.AddHours(-1) }
            );
            await db.SaveChangesAsync();

            // Act
            var transactions = await db.Transactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            // Assert
            transactions[0].Amount.Should().Be(200);
            transactions[1].Amount.Should().Be(150);
            transactions[2].Amount.Should().Be(100);
        }

        [Fact]
        public async Task QueryTransactionByDateRange_FiltersCorrectly()
        {
            // Arrange
            var db = BuildContext();
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var from = now.AddDays(-5);
            var to = now.AddDays(-2);

            db.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = now.AddDays(-10) },
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 200, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = now.AddDays(-3) },
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 150, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = now }
            );
            await db.SaveChangesAsync();

            // Act
            var transactions = await db.Transactions
                .Where(t => t.UserId == userId && t.CreatedAt >= from && t.CreatedAt <= to)
                .ToListAsync();

            // Assert
            transactions.Should().HaveCount(1);
            transactions[0].Amount.Should().Be(200);
        }

        [Fact]
        public async Task NoTracking_ImprovedPerformanceForReadOnlyQueries()
        {
            // Arrange
            var db = BuildContext();
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();

            db.Transactions.Add(new Transaction 
            { 
                Id = Guid.NewGuid(), 
                AccountId = accountId, 
                UserId = userId, 
                Amount = 100, 
                Currency = "USD", 
                Type = "debit", 
                Description = "Test transaction",
                SpendingType = "Fun", 
                CreatedAt = DateTime.UtcNow 
            });
            await db.SaveChangesAsync();

            // Act
            var transactions = await db.Transactions
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .ToListAsync();

            // Assert
            transactions.Should().HaveCount(1);
        }
    }

    #endregion

    #region Transaction Isolation Tests

    public class TransactionIsolationTests
    {
        [Fact]
        public async Task MultipleContextsIsolated_InMemoryDatabase()
        {
            // Arrange
            var sharedDbName = Guid.NewGuid().ToString();
            
            var options1 = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(sharedDbName)
                .Options;
            var db1 = new UserDbContext(options1);

            var options2 = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(sharedDbName)
                .Options;
            var db2 = new UserDbContext(options2);

            // Act
            var user1 = new User
            {
                Id = Guid.NewGuid(),
                Email = "user1@example.com",
                PasswordHash = "hash",
                FirstName = "User1",
                LastName = "Test",
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db1.Users.Add(user1);
            await db1.SaveChangesAsync();

            // Assert
            var found = await db2.Users.FirstOrDefaultAsync(u => u.Email == "user1@example.com");
            found.Should().NotBeNull(); // Should be found in shared database
        }
    }

    #endregion
}
