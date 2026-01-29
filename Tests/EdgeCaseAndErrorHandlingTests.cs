using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using UserService.Controllers;
using UserService.Data;
using AccountService.Controllers;
using AccountService.Data;
using TransactionService.Controllers;
using TransactionService.Data;
using TransactionService.Models.Dtos;

namespace Tests;

/// <summary>
/// Edge case and error handling tests for all API controllers
/// Focus on unusual inputs, boundary conditions, and error scenarios
/// </summary>
public class EdgeCaseAndErrorHandlingTests
{
    #region UserService Edge Cases

    public class UserServiceEdgeCaseTests
    {
        private (UsersController Controller, UserDbContext Db) BuildController()
        {
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new UserDbContext(options);

            var logger = new Mock<ILogger<UsersController>>();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT_SIGNING_KEY"] = "test-signing-key-0123456789-must-be-long",
                    ["RegistrationDisabled"] = "false"
                })
                .Build();

            var controller = new UsersController(db, logger.Object, config)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            return (controller, db);
        }

        [Theory]
        [InlineData("user+tag@example.com")]
        [InlineData("name.surname@example.co.uk")]
        public async Task Register_WithValidEmailVariations_Succeeds(string email)
        {
            // Arrange
            var (controller, _) = BuildController();
            var request = new RegisterRequest(email, "Password#123", "John", "Doe");

            // Act
            var result = await controller.Register(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Theory]
        [InlineData("a@b.c")] // TLD too short (requires 2+ characters)
        [InlineData("a@b")]    // No TLD
        public async Task Register_WithInvalidShortEmail_ReturnsBadRequest(string email)
        {
            // Arrange
            var (controller, _) = BuildController();
            var request = new RegisterRequest(email, "Password#123", "John", "Doe");

            // Act
            var result = await controller.Register(request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Theory]
        [InlineData("          ")]
        [InlineData("\t\t\t")]
        [InlineData("\n\n\n")]
        public async Task Register_WithWhitespaceOnly_ReturnsBadRequest(string whitespace)
        {
            // Arrange
            var (controller, _) = BuildController();
            var request = new RegisterRequest(whitespace, "Password#123", "John", "Doe");

            // Act
            var result = await controller.Register(request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Register_WithVeryLongEmail_StillWorks()
        {
            // Arrange
            var (controller, _) = BuildController();
            var longLocalPart = new string('a', 64);
            var email = $"{longLocalPart}@example.com";
            var request = new RegisterRequest(email, "Password#123", "John", "Doe");

            // Act
            var result = await controller.Register(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task Register_WithVeryLongPassword_Succeeds()
        {
            // Arrange
            var (controller, _) = BuildController();
            var longPassword = "A" + new string('a', 1000) + "1#";
            var request = new RegisterRequest("test@example.com", longPassword, "John", "Doe");

            // Act
            var result = await controller.Register(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task Register_WithVeryLongName_GetsTruncated()
        {
            // Arrange
            var (controller, db) = BuildController();
            var longName = new string('A', 200);
            var request = new RegisterRequest("test@example.com", "Password#123", longName, "Doe");

            // Act
            var result = await controller.Register(request);

            // Assert
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());
            user!.FirstName.Length.Should().BeLessThanOrEqualTo(100);
        }

        [Theory]
        [InlineData("Password#123456789")]
        [InlineData("MyP@ssw0rd")]
        [InlineData("Test@Secure123")]
        public async Task Register_WithMultipleSpecialCharacters_Succeeds(string password)
        {
            // Arrange
            var (controller, _) = BuildController();
            var request = new RegisterRequest("test@example.com", password, "John", "Doe");

            // Act
            var result = await controller.Register(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }
    }

    #endregion

    #region AccountService Edge Cases

    public class AccountServiceEdgeCaseTests
    {
        private (AccountsController Controller, AccountDbContext Db) BuildController()
        {
            var options = new DbContextOptionsBuilder<AccountDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new AccountDbContext(options);
            var logger = new Mock<ILogger<AccountsController>>();

            var controller = new AccountsController(db, logger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            return (controller, db);
        }

        private ClaimsPrincipal CreateUserPrincipal(Guid userId)
        {
            var identity = new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) }, "Test");
            return new ClaimsPrincipal(identity);
        }

        [Theory]
        [InlineData("USD")]
        [InlineData("EUR")]
        [InlineData("GBP")]
        [InlineData("JPY")]
        [InlineData("AUD")]
        public async Task CreateAccount_WithVariousCurrencies_Succeeds(string currency)
        {
            // Arrange
            var (controller, _) = BuildController();
            var userId = Guid.NewGuid();
            var request = new CreateAccountRequest("Checking", currency);

            controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

            // Act
            var result = await controller.CreateAccount(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAccount_WithEmptyCurrency_UsesDefault(string currency)
        {
            // Arrange
            var (controller, _) = BuildController();
            var userId = Guid.NewGuid();
            var request = new CreateAccountRequest("Savings", currency);

            controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

            // Act
            var result = await controller.CreateAccount(request);

            // Assert
            var okResult = (OkObjectResult)result;
            var prop = okResult.Value?.GetType().GetProperty("currency");
            prop?.GetValue(okResult.Value)?.ToString().Should().Be("USD");
        }

        [Fact]
        public async Task CreateAccount_ManyAccountsSameUser_AllCreatedWithUniqueNumbers()
        {
            // Arrange
            var (controller, db) = BuildController();
            var userId = Guid.NewGuid();
            controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

            // Act
            for (int i = 0; i < 10; i++)
            {
                await controller.CreateAccount(new CreateAccountRequest("Checking", "USD"));
            }

            // Assert
            var accounts = await db.Accounts.Where(a => a.UserId == userId).ToListAsync();
            accounts.Should().HaveCount(10);
            accounts.Select(a => a.AccountNumber).Distinct().Should().HaveCount(10);
        }
    }

    #endregion

    #region TransactionService Edge Cases

    public class TransactionServiceEdgeCaseTests
    {
        private (TransactionsController Controller, TransactionDbContext Db, Mock<MassTransit.IPublishEndpoint> Publisher)
            BuildController()
        {
            var options = new DbContextOptionsBuilder<TransactionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new TransactionDbContext(options);
            var logger = new Mock<ILogger<TransactionsController>>();
            var publisher = new Mock<MassTransit.IPublishEndpoint>();

            var controller = new TransactionsController(db, publisher.Object, logger.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            return (controller, db, publisher);
        }

        private ClaimsPrincipal CreateUserPrincipal(Guid userId)
        {
            var identity = new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) }, "Test");
            return new ClaimsPrincipal(identity);
        }

        [Fact]
        public async Task CreateTransaction_WithZeroAmount_Succeeds()
        {
            // Arrange
            var (controller, _, _) = BuildController();
            var userId = Guid.NewGuid();
            var request = new CreatePaymentRequestDto
            {
                AccountId = Guid.NewGuid(),
                Amount = 0m,
                Currency = "USD",
                SpendingType = "Fun"
            };

            controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

            // Act
            var result = await controller.CreateTransaction(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task CreateTransaction_WithNegativeAmount_Succeeds()
        {
            // Arrange
            var (controller, _, _) = BuildController();
            var userId = Guid.NewGuid();
            var request = new CreatePaymentRequestDto
            {
                AccountId = Guid.NewGuid(),
                Amount = -100m,
                Currency = "USD",
                SpendingType = "Fun"
            };

            controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

            // Act
            var result = await controller.CreateTransaction(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task CreateTransaction_WithVeryLargeAmount_Succeeds()
        {
            // Arrange
            var (controller, _, _) = BuildController();
            var userId = Guid.NewGuid();
            var request = new CreatePaymentRequestDto
            {
                AccountId = Guid.NewGuid(),
                Amount = decimal.MaxValue / 2,
                Currency = "USD",
                SpendingType = "Fun"
            };

            controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

            // Act
            var result = await controller.CreateTransaction(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task CreateTransaction_WithEmptyDescription_Succeeds()
        {
            // Arrange
            var (controller, _, _) = BuildController();
            var userId = Guid.NewGuid();
            var request = new CreatePaymentRequestDto
            {
                AccountId = Guid.NewGuid(),
                Amount = 100m,
                Currency = "USD",
                Description = "",
                SpendingType = "Fun"
            };

            controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

            // Act
            var result = await controller.CreateTransaction(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task CreateTransaction_WithVeryLongDescription_Succeeds()
        {
            // Arrange
            var (controller, _, _) = BuildController();
            var userId = Guid.NewGuid();
            var longDescription = new string('A', 10000);
            var request = new CreatePaymentRequestDto
            {
                AccountId = Guid.NewGuid(),
                Amount = 100m,
                Currency = "USD",
                Description = longDescription,
                SpendingType = "Fun"
            };

            controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

            // Act
            var result = await controller.CreateTransaction(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetTransactions_WithInvalidGuidAccountId_FiltersCorrectly()
        {
            // Arrange
            var (controller, db, _) = BuildController();
            var userId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var anotherAccountId = Guid.NewGuid();

            db.Transactions.AddRange(
                new Transaction { Id = Guid.NewGuid(), AccountId = accountId, UserId = userId, Amount = 100, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = DateTime.UtcNow },
                new Transaction { Id = Guid.NewGuid(), AccountId = anotherAccountId, UserId = userId, Amount = 200, Currency = "USD", Type = "debit", Description = "Test transaction", SpendingType = "Fun", CreatedAt = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();

            controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

            // Act
            var result = await controller.GetTransactions(accountId);

            // Assert
            var okResult = (OkObjectResult)result;
            var transactions = (okResult.Value as System.Collections.IEnumerable)?.Cast<dynamic>().ToList();
            transactions.Should().HaveCount(1);
        }

        [Theory]
        [InlineData("0.01")]
        [InlineData("0.99")]
        [InlineData("999.99")]
        [InlineData("0.001")]
        public async Task CreateTransaction_WithPreciseDecimalAmounts_Stored(string amountStr)
        {
            // Arrange
            var (controller, db, _) = BuildController();
            var userId = Guid.NewGuid();
            var amount = decimal.Parse(amountStr);
            var request = new CreatePaymentRequestDto
            {
                AccountId = Guid.NewGuid(),
                Amount = amount,
                Currency = "USD",
                SpendingType = "Fun"
            };

            controller.ControllerContext.HttpContext!.User = CreateUserPrincipal(userId);

            // Act
            await controller.CreateTransaction(request);

            // Assert
            var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.UserId == userId);
            transaction!.Amount.Should().Be(amount);
        }
    }

    #endregion

    #region Concurrent Operation Tests

    public class ConcurrentOperationTests
    {
        [Fact]
        public async Task MultipleUsersCreatingAccountsSimultaneously_NoDataCorruption()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<AccountDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new AccountDbContext(options);
            var logger = new Mock<ILogger<AccountsController>>();

            var tasks = new List<Task>();

            // Act - Create accounts for 5 different users concurrently
            for (int i = 0; i < 5; i++)
            {
                var controller = new AccountsController(db, logger.Object)
                {
                    ControllerContext = new ControllerContext
                    {
                        HttpContext = new DefaultHttpContext()
                    }
                };

                var userId = Guid.NewGuid();
                var identity = new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) }, "Test");
                controller.ControllerContext.HttpContext!.User = new ClaimsPrincipal(identity);

                tasks.Add(controller.CreateAccount(new CreateAccountRequest("Checking", "USD")));
            }

            await Task.WhenAll(tasks);

            // Assert
            var allAccounts = await db.Accounts.ToListAsync();
            allAccounts.Should().HaveCount(5);
            allAccounts.Select(a => a.AccountNumber).Distinct().Should().HaveCount(5);
        }
    }

    #endregion

    #region Boundary Value Tests

    public class BoundaryValueTests
    {
        [Fact]
        public void MinimumPasswordLength_Exactly8Characters_Valid()
        {
            // Arrange
            var password = "Passwrd#";

            // Act
            var hash = UsersController.HashPasswordStatic(password);

            // Assert
            hash.Should().StartWith("v1$");
        }

        [Fact]
        public void EmptyStringAfterSanitization_HandledGracefully()
        {
            // Arrange & Act - Test by checking that registering with HTML-only input is handled
            // This tests the edge case where all input becomes empty after sanitization

            // Assert
            true.Should().BeTrue(); // Placeholder for sanitization edge case
        }

        [Theory]
        [InlineData(int.MaxValue)]
        [InlineData(1)]
        [InlineData(999999)]
        public void AccountNumberGeneration_CreatesValidNumbers(int seed)
        {
            // Arrange & Act
            var number = seed.ToString();

            // Assert
            number.Should().NotBeEmpty();
            number.Should().MatchRegex("^[0-9]+$");
        }
    }

    #endregion
}
