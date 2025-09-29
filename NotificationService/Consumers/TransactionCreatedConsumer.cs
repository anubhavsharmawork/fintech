using MassTransit;
using Contracts.Events;

namespace NotificationService.Consumers;

public class TransactionCreatedConsumer : IConsumer<TransactionCreated>
{
    private readonly ILogger<TransactionCreatedConsumer> _logger;

    public TransactionCreatedConsumer(ILogger<TransactionCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TransactionCreated> context)
    {
        var transaction = context.Message;
        
        _logger.LogInformation("Processing transaction notification: {TransactionId} for user {UserId}", 
            transaction.TransactionId, transaction.UserId);

        // Simulate email notification
        await SendEmailNotification(transaction);
        
        // Simulate SMS notification
        await SendSmsNotification(transaction);
        
        _logger.LogInformation("Notification sent for transaction: {TransactionId}", 
            transaction.TransactionId);
    }

    private async Task SendEmailNotification(TransactionCreated transaction)
    {
        // Email notification stub
        await Task.Delay(100); // Simulate email sending delay
        
        _logger.LogInformation("Email notification sent for transaction {TransactionId}: {Type} of {Amount} {Currency}",
            transaction.TransactionId,
            transaction.Type,
            transaction.Amount,
            transaction.Currency);
    }

    private async Task SendSmsNotification(TransactionCreated transaction)
    {
        // SMS notification stub
        await Task.Delay(50); // Simulate SMS sending delay
        
        _logger.LogInformation("SMS notification sent for transaction {TransactionId}: {Type} of {Amount} {Currency}",
            transaction.TransactionId,
            transaction.Type,
            transaction.Amount,
            transaction.Currency);
    }
}