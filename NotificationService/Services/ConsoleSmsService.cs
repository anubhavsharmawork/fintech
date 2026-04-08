namespace NotificationService.Services;

public class ConsoleSmsService : ISmsService
{
    private readonly ILogger<ConsoleSmsService> _logger;

    public ConsoleSmsService(ILogger<ConsoleSmsService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string phoneNumber, string message)
    {
        _logger.LogInformation("[SMS] To={PhoneNumber} Message={Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
