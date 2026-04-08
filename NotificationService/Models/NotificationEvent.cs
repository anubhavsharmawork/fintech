namespace NotificationService.Models;

public record NotificationEvent(
    Guid Id,
    string EventType,
    string Message,
    DateTime Timestamp,
    bool Read
);
