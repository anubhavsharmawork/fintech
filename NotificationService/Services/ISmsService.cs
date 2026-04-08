namespace NotificationService.Services;

public interface ISmsService
{
    Task SendAsync(string phoneNumber, string message);
}
