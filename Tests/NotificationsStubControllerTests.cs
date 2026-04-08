using ApiGateway.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Tests;

public class NotificationsStubControllerTests
{
    [Fact]
    public void GetNotifications_ReturnsEmptyArray()
    {
        // Arrange
        var controller = new NotificationsStubController();

        // Act
        var result = controller.GetNotifications();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var notifications = Assert.IsType<object[]>(okResult.Value);
        notifications.Should().BeEmpty();
    }

    [Fact]
    public void MarkAllRead_ReturnsOkWithEmptyObject()
    {
        // Arrange
        var controller = new NotificationsStubController();

        // Act
        var result = controller.MarkAllRead();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void GetPreferences_ReturnsDefaultPreferences()
    {
        // Arrange
        var controller = new NotificationsStubController();

        // Act
        var result = controller.GetPreferences();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void GetPreferences_ContainsExpectedEventTypes()
    {
        // Arrange
        var controller = new NotificationsStubController();

        // Act
        var result = controller.GetPreferences();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var preferences = okResult.Value;
        var json = System.Text.Json.JsonSerializer.Serialize(preferences);

        json.Should().Contain("TransactionCreated");
        json.Should().Contain("PaymentApproved");
        json.Should().Contain("PaymentBatchSubmittedForApproval");
    }

    [Fact]
    public void SavePreferences_ReturnsOk()
    {
        // Arrange
        var controller = new NotificationsStubController();
        var body = System.Text.Json.JsonDocument.Parse("{}").RootElement;

        // Act
        var result = controller.SavePreferences(body);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public void SavePreferences_AcceptsComplexPayload()
    {
        // Arrange
        var controller = new NotificationsStubController();
        var json = @"{
            ""preferences"": [
                { ""eventType"": ""TransactionCreated"", ""emailEnabled"": true }
            ]
        }";
        var body = System.Text.Json.JsonDocument.Parse(json).RootElement;

        // Act
        var result = controller.SavePreferences(body);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }
}
