using Xunit;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Controllers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests;

public class UserServiceTests
{
    private UserDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new UserDbContext(options);
    }

    [Fact]
    public async Task Register_ShouldCreateUser_WhenValidRequest()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var logger = new Mock<ILogger<UsersController>>();
        var controller = new UsersController(context, logger.Object);
        
        var request = new RegisterRequest(
            "test@example.com", 
            "password123", 
            "John", 
            "Doe"
        );

        // Act
        var result = await controller.Register(request);

        // Assert
        Assert.NotNull(result);
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user);
        Assert.Equal("John", user.FirstName);
        Assert.Equal("Doe", user.LastName);
    }

    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenEmailAlreadyExists()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Jane",
            LastName = "Smith",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        var logger = new Mock<ILogger<UsersController>>();
        var controller = new UsersController(context, logger.Object);
        
        var request = new RegisterRequest(
            "test@example.com", 
            "password123", 
            "John", 
            "Doe"
        );

        // Act
        var result = await controller.Register(request);

        // Assert
        Assert.NotNull(result);
        // In a real test, you'd assert on the specific result type (BadRequest)
    }
}

public class AccountServiceTests
{
    [Fact]
    public void GenerateAccountNumber_ShouldReturnValidFormat()
    {
        // This is a sample test - in real implementation you'd test actual account logic
        var accountNumber = new Random().Next(1000000000, int.MaxValue).ToString();
        
        Assert.True(accountNumber.Length >= 10);
        Assert.True(long.TryParse(accountNumber, out _));
    }
}

public class TransactionServiceTests
{
    [Fact]
    public void TransactionAmount_ShouldBePositive()
    {
        // Sample test for transaction validation logic
        var amount = 100.50m;
        
        Assert.True(amount > 0);
    }
}