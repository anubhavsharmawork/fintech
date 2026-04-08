using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Shared request types for the three additional service filter tests
// ─────────────────────────────────────────────────────────────────────────────
public record AccountServiceFilterRequest(string AccountName, decimal Balance);
public record UserServiceFilterRequest(string Username, string Email);
public record CorporateFilterRequest(string OrganisationName, string RegistrationNumber);

// ─────────────────────────────────────────────────────────────────────────────
// AccountService.Validation.FluentValidationFilter
// ─────────────────────────────────────────────────────────────────────────────
public class AccountServiceFluentValidationFilterTests
{
    private static ActionExecutingContext MakeContext(IServiceProvider provider, object? argument)
    {
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var args = argument is not null
            ? new Dictionary<string, object?> { ["model"] = argument }
            : new Dictionary<string, object?>();
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), args, controller: new object());
    }

    [Fact]
    public async Task OnActionExecutionAsync_ValidRequest_CallsNext()
    {
        var validator = new Mock<IValidator<AccountServiceFilterRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult());

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<AccountServiceFilterRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new AccountService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new AccountServiceFilterRequest("Savings", 1000m));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeTrue();
        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_InvalidRequest_ReturnsBadRequest()
    {
        var failures = new List<ValidationFailure>
        {
            new("AccountName", "Account name is required"),
            new("Balance", "Balance must be non-negative")
        };
        var validator = new Mock<IValidator<AccountServiceFilterRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<AccountServiceFilterRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new AccountService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new AccountServiceFilterRequest("", -1m));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeFalse();
        var badRequest = Assert.IsType<BadRequestObjectResult>(ctx.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        details.Errors.Should().ContainKey("AccountName");
        details.Errors.Should().ContainKey("Balance");
    }

    [Fact]
    public async Task OnActionExecutionAsync_MultipleErrorsPerProperty_AreAggregated()
    {
        var failures = new List<ValidationFailure>
        {
            new("AccountName", "Account name is required"),
            new("AccountName", "Account name must be at most 100 characters")
        };
        var validator = new Mock<IValidator<AccountServiceFilterRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<AccountServiceFilterRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new AccountService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new AccountServiceFilterRequest("", 0m));

        await filter.OnActionExecutionAsync(ctx, () => Task.FromResult<ActionExecutedContext>(null!));

        var badRequest = Assert.IsType<BadRequestObjectResult>(ctx.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        details.Errors["AccountName"].Should().HaveCount(2);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoValidatorRegistered_CallsNext()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var filter = new AccountService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new AccountServiceFilterRequest("Savings", 500m));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeTrue();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// UserService.Validation.FluentValidationFilter
// ─────────────────────────────────────────────────────────────────────────────
public class UserServiceFluentValidationFilterTests
{
    private static ActionExecutingContext MakeContext(IServiceProvider provider, object? argument)
    {
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var args = argument is not null
            ? new Dictionary<string, object?> { ["model"] = argument }
            : new Dictionary<string, object?>();
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), args, controller: new object());
    }

    [Fact]
    public async Task OnActionExecutionAsync_ValidRequest_CallsNext()
    {
        var validator = new Mock<IValidator<UserServiceFilterRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult());

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<UserServiceFilterRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new UserService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new UserServiceFilterRequest("alice", "alice@example.com"));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeTrue();
        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_InvalidRequest_ReturnsBadRequest()
    {
        var failures = new List<ValidationFailure>
        {
            new("Username", "Username is required"),
            new("Email", "Email is not valid")
        };
        var validator = new Mock<IValidator<UserServiceFilterRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<UserServiceFilterRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new UserService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new UserServiceFilterRequest("", "not-an-email"));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeFalse();
        var badRequest = Assert.IsType<BadRequestObjectResult>(ctx.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        details.Errors.Should().ContainKey("Username");
        details.Errors.Should().ContainKey("Email");
    }

    [Fact]
    public async Task OnActionExecutionAsync_MultipleErrorsPerProperty_AreAggregated()
    {
        var failures = new List<ValidationFailure>
        {
            new("Email", "Email is required"),
            new("Email", "Email must be a valid address")
        };
        var validator = new Mock<IValidator<UserServiceFilterRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<UserServiceFilterRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new UserService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new UserServiceFilterRequest("bob", ""));

        await filter.OnActionExecutionAsync(ctx, () => Task.FromResult<ActionExecutedContext>(null!));

        var badRequest = Assert.IsType<BadRequestObjectResult>(ctx.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        details.Errors["Email"].Should().HaveCount(2);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoValidatorRegistered_CallsNext()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var filter = new UserService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new UserServiceFilterRequest("alice", "alice@example.com"));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeTrue();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CorporateBankingService.Validation.FluentValidationFilter
// ─────────────────────────────────────────────────────────────────────────────
public class CorporateFluentValidationFilterTests
{
    private static ActionExecutingContext MakeContext(IServiceProvider provider, object? argument)
    {
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var args = argument is not null
            ? new Dictionary<string, object?> { ["model"] = argument }
            : new Dictionary<string, object?>();
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), args, controller: new object());
    }

    [Fact]
    public async Task OnActionExecutionAsync_ValidRequest_CallsNext()
    {
        var validator = new Mock<IValidator<CorporateFilterRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult());

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<CorporateFilterRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new CorporateBankingService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new CorporateFilterRequest("Acme Corp", "REG123"));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeTrue();
        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_InvalidRequest_ReturnsBadRequest()
    {
        var failures = new List<ValidationFailure>
        {
            new("OrganisationName", "Organisation name is required"),
            new("RegistrationNumber", "Registration number is required")
        };
        var validator = new Mock<IValidator<CorporateFilterRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<CorporateFilterRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new CorporateBankingService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new CorporateFilterRequest("", ""));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeFalse();
        var badRequest = Assert.IsType<BadRequestObjectResult>(ctx.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        details.Errors.Should().ContainKey("OrganisationName");
        details.Errors.Should().ContainKey("RegistrationNumber");
    }

    [Fact]
    public async Task OnActionExecutionAsync_MultipleErrorsPerProperty_AreAggregated()
    {
        var failures = new List<ValidationFailure>
        {
            new("OrganisationName", "Organisation name is required"),
            new("OrganisationName", "Organisation name must be at most 200 characters")
        };
        var validator = new Mock<IValidator<CorporateFilterRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<CorporateFilterRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new CorporateBankingService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new CorporateFilterRequest("", "REG456"));

        await filter.OnActionExecutionAsync(ctx, () => Task.FromResult<ActionExecutedContext>(null!));

        var badRequest = Assert.IsType<BadRequestObjectResult>(ctx.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        details.Errors["OrganisationName"].Should().HaveCount(2);
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoValidatorRegistered_CallsNext()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var filter = new CorporateBankingService.Validation.FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new CorporateFilterRequest("Acme Corp", "REG123"));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeTrue();
    }
}
