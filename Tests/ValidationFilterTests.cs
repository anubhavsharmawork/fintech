using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TransactionService.Validation;

namespace Tests;

// Public top-level types — required so Moq/Castle can proxy IValidator<T>
// across assembly boundaries when FluentValidation is strong-named.
public record FluentFilterTestRequest(string Name, int Age);
public record ApiGatewayFilterRequest(string Email);

// ─────────────────────────────────────────────────────────────────────────────
// TransactionService FluentValidationFilter (IAsyncActionFilter)
// ─────────────────────────────────────────────────────────────────────────────

public class FluentValidationFilterTests
{
    private static ActionExecutingContext MakeContext(
        IServiceProvider provider, object? argument)
    {
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new ActionContext(
            httpContext, new RouteData(), new ActionDescriptor());

        var args = argument is not null
            ? new Dictionary<string, object?> { ["model"] = argument }
            : new Dictionary<string, object?>();

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            args,
            controller: new object());
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoArguments_CallsNext()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var filter = new FluentValidationFilter(provider);
        var ctx = MakeContext(provider, null);
        ctx.ActionArguments.Clear();

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_NullArgument_CallsNext()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var filter = new FluentValidationFilter(provider);
        var ctx = MakeContext(provider, null);

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoValidatorRegistered_CallsNext()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var filter = new FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new FluentFilterTestRequest("Alice", 30));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ValidRequest_CallsNext()
    {
        var validator = new Mock<IValidator<FluentFilterTestRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult());

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentFilterTestRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new FluentFilterTestRequest("Alice", 30));

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
            new("Name", "Name is required"),
            new("Age", "Age must be positive")
        };
        var validator = new Mock<IValidator<FluentFilterTestRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentFilterTestRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new FluentFilterTestRequest("", -1));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(ctx, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        nextCalled.Should().BeFalse();
        var badRequest = Assert.IsType<BadRequestObjectResult>(ctx.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        details.Errors.Should().ContainKey("Name");
        details.Errors.Should().ContainKey("Age");
    }

    [Fact]
    public async Task OnActionExecutionAsync_InvalidRequest_GroupsMultipleErrorsPerProperty()
    {
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required"),
            new("Name", "Name must be at least 2 characters")
        };
        var validator = new Mock<IValidator<FluentFilterTestRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentFilterTestRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new FluentFilterTestRequest("", 0));

        await filter.OnActionExecutionAsync(ctx, () => Task.FromResult<ActionExecutedContext>(null!));

        var badRequest = Assert.IsType<BadRequestObjectResult>(ctx.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        details.Errors["Name"].Should().HaveCount(2);
    }

    [Fact]
    public async Task OnActionExecutionAsync_InvalidRequest_SetsTitle()
    {
        var failures = new List<ValidationFailure> { new("Name", "Required") };
        var validator = new Mock<IValidator<FluentFilterTestRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentFilterTestRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new FluentFilterTestRequest("", 0));

        await filter.OnActionExecutionAsync(ctx, () => Task.FromResult<ActionExecutedContext>(null!));

        var badRequest = Assert.IsType<BadRequestObjectResult>(ctx.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        details.Title.Should().Be("One or more validation errors occurred.");
    }

    [Fact]
    public async Task OnActionExecutionAsync_InvalidRequest_SetsTypeUrl()
    {
        var failures = new List<ValidationFailure> { new("Name", "Required") };
        var validator = new Mock<IValidator<FluentFilterTestRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), default))
            .ReturnsAsync(new ValidationResult(failures));

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<FluentFilterTestRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var filter = new FluentValidationFilter(provider);
        var ctx = MakeContext(provider, new FluentFilterTestRequest("", 0));

        await filter.OnActionExecutionAsync(ctx, () => Task.FromResult<ActionExecutedContext>(null!));

        var badRequest = Assert.IsType<BadRequestObjectResult>(ctx.Result);
        var details = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        details.Type.Should().Contain("rfc9110");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ApiGateway ValidationFilter<TRequest> (IEndpointFilter)
// ─────────────────────────────────────────────────────────────────────────────

public class ApiGatewayValidationFilterTests
{
    private static async Task<object?> InvokeFilter(
        IServiceProvider services,
        object? argument)
    {
        var filterType = typeof(ApiGateway.Controllers.FeedbackController).Assembly
            .GetTypes()
            .First(t => t.Name == "ValidationFilter`1")
            .MakeGenericType(typeof(ApiGatewayFilterRequest));

        var filter = (IEndpointFilter)Activator.CreateInstance(filterType)!;

        var httpContext = new DefaultHttpContext { RequestServices = services };

        // EndpointFilterInvocationContext.Create takes params, so pass argument directly
        EndpointFilterInvocationContext ctx;
        if (argument is null)
        {
            ctx = EndpointFilterInvocationContext.Create(httpContext);
        }
        else
        {
            ctx = EndpointFilterInvocationContext.Create(httpContext, argument);
        }

        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(Results.Ok());

        return await filter.InvokeAsync(ctx, next);
    }

    [Fact]
    public async Task InvokeAsync_NoValidatorRegistered_CallsNext()
    {
        var provider = new ServiceCollection().BuildServiceProvider();

        var result = await InvokeFilter(provider, new ApiGatewayFilterRequest("test@example.com"));

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_NullArgument_CallsNext()
    {
        var provider = new ServiceCollection().BuildServiceProvider();

        var result = await InvokeFilter(provider, null);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_NonMatchingArgumentType_CallsNext()
    {
        var provider = new ServiceCollection().BuildServiceProvider();

        var result = await InvokeFilter(provider, "not-a-request");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_ValidRequest_CallsNext()
    {
        var validator = new Mock<IValidator<ApiGatewayFilterRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ApiGatewayFilterRequest>(), default))
            .ReturnsAsync(new ValidationResult());

        var services = new ServiceCollection();
        services.AddSingleton(validator.Object);
        var provider = services.BuildServiceProvider();

        var result = await InvokeFilter(provider, new ApiGatewayFilterRequest("good@example.com"));

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_InvalidRequest_ReturnsValidationProblemResult()
    {
        var failures = new List<ValidationFailure>
        {
            new("Email", "Email is required"),
            new("Email", "Email must be valid")
        };
        var validator = new Mock<IValidator<ApiGatewayFilterRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ApiGatewayFilterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(failures));

        var services = new ServiceCollection();
        services.AddSingleton<IValidator<ApiGatewayFilterRequest>>(validator.Object);
        var provider = services.BuildServiceProvider();

        var result = await InvokeFilter(provider, new ApiGatewayFilterRequest(""));

        result.Should().BeAssignableTo<IResult>();
        result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>();
    }
}
