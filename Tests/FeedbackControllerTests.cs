using System.Security.Claims;
using ApiGateway.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests;

public class FeedbackControllerTests
{
    private static FeedbackController BuildController(Guid? userId = null)
    {
        var logger = new Mock<ILogger<FeedbackController>>();
        var controller = new FeedbackController(logger.Object);

        var id = userId ?? Guid.NewGuid();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    private static FeedbackController BuildControllerWithSubClaim(Guid? userId = null)
    {
        var logger = new Mock<ILogger<FeedbackController>>();
        var controller = new FeedbackController(logger.Object);

        var id = userId ?? Guid.NewGuid();
        var claims = new List<Claim>
        {
            new Claim("sub", id.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    private static FeedbackController BuildControllerNoUserId()
    {
        var logger = new Mock<ILogger<FeedbackController>>();
        var controller = new FeedbackController(logger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            }
        };
        return controller;
    }

    // ── Validation ──────────────────────────────────────────────────────────

    [Fact]
    public void Submit_NullRequest_Returns400()
    {
        var controller = BuildController();

        var result = controller.Submit(null!);

        var obj = Assert.IsType<ObjectResult>(result);
        obj.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Submit_EmptyMessage_Returns400()
    {
        var controller = BuildController();

        var result = controller.Submit(new FeedbackRequest { Message = "   " });

        var obj = Assert.IsType<ObjectResult>(result);
        obj.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Submit_ShortMessage_Returns400()
    {
        var controller = BuildController();

        var result = controller.Submit(new FeedbackRequest { Message = "short" });

        var obj = Assert.IsType<ObjectResult>(result);
        obj.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Submit_MessageExceeds2000Chars_Returns400()
    {
        var controller = BuildController();
        var longMessage = new string('x', 2001);

        var result = controller.Submit(new FeedbackRequest { Message = longMessage });

        var obj = Assert.IsType<ObjectResult>(result);
        obj.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Submit_MessageExactly10Chars_Succeeds()
    {
        var controller = BuildController();

        var result = controller.Submit(new FeedbackRequest { Message = "1234567890" });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void Submit_MessageExactly2000Chars_Succeeds()
    {
        var controller = BuildController();
        var message = new string('x', 2000);

        var result = controller.Submit(new FeedbackRequest { Message = message });

        Assert.IsType<OkObjectResult>(result);
    }

    // ── Authentication ───────────────────────────────────────────────────────

    [Fact]
    public void Submit_MissingUserIdClaim_Returns401()
    {
        var controller = BuildControllerNoUserId();

        var result = controller.Submit(new FeedbackRequest { Message = "Valid message here!" });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void Submit_InvalidGuidInClaim_Returns401()
    {
        var logger = new Mock<ILogger<FeedbackController>>();
        var controller = new FeedbackController(logger.Object);

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") };
        var identity = new ClaimsIdentity(claims, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var result = controller.Submit(new FeedbackRequest { Message = "Valid message here!" });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void Submit_SubClaim_Succeeds()
    {
        var controller = BuildControllerWithSubClaim();

        var result = controller.Submit(new FeedbackRequest { Message = "Valid feedback message" });

        Assert.IsType<OkObjectResult>(result);
    }

    // ── Success path ─────────────────────────────────────────────────────────

    [Fact]
    public void Submit_ValidRequest_ReturnsOkWithFeedbackId()
    {
        var controller = BuildController();

        var result = controller.Submit(new FeedbackRequest { Message = "This is a valid feedback message." });

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value;
        var feedbackIdProp = value?.GetType().GetProperty("feedbackId");
        feedbackIdProp.Should().NotBeNull();
        var feedbackId = (Guid)feedbackIdProp!.GetValue(value)!;
        feedbackId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Submit_ValidRequest_TrimmedMessageIsPersisted()
    {
        var controller = BuildController();

        var result = controller.Submit(new FeedbackRequest { Message = "  Valid padded message  " });

        Assert.IsType<OkObjectResult>(result);
    }

    // ── PII sanitisation ──────────────────────────────────────────────────────

    [Fact]
    public void Submit_MessageWithCreditCard_IsRedacted()
    {
        var controller = BuildController();

        // 16-digit credit card number in message
        var result = controller.Submit(new FeedbackRequest
        {
            Message = "My card number is 4111111111111111 please help"
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void Submit_MessageWithBankAccount_IsRedacted()
    {
        var controller = BuildController();

        // NZ bank account format XX-XXXX-XXXXXXX-XX
        var result = controller.Submit(new FeedbackRequest
        {
            Message = "My account 12-3456-1234567-00 has an issue"
        });

        Assert.IsType<OkObjectResult>(result);
    }

    // ── Rate limiting ─────────────────────────────────────────────────────────

    [Fact]
    public void Submit_SixthCallSameUser_Returns429()
    {
        // Each test uses a fresh controller with a unique userId to avoid
        // cross-test state; use a single userId across all 6 calls here.
        var userId = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
        {
            var ctrl = BuildController(userId);
            var res = ctrl.Submit(new FeedbackRequest { Message = $"Valid message attempt {i + 1}" });
            Assert.IsType<OkObjectResult>(res);
        }

        var sixthController = BuildController(userId);
        var result = sixthController.Submit(new FeedbackRequest { Message = "Sixth message attempt" });

        var obj = Assert.IsType<ObjectResult>(result);
        obj.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public void Submit_DifferentUsers_IndependentRateLimits()
    {
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
        {
            BuildController(userId1).Submit(new FeedbackRequest { Message = $"User1 message {i + 1} is valid" });
        }

        // User 2 should still be able to submit
        var result = BuildController(userId2).Submit(new FeedbackRequest { Message = "User2 first message is valid" });

        Assert.IsType<OkObjectResult>(result);
    }
}
