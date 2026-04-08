using Contracts.Events;
using FluentEmail.Core;
using MassTransit;
using NotificationService.Services;
using NotificationService.Stores;

namespace NotificationService.Consumers;

public class TransactionCreatedConsumer : IConsumer<TransactionCreated>
{
    private readonly ILogger<TransactionCreatedConsumer> _logger;
    private readonly IFluentEmail _fluentEmail;
    private readonly ISmsService _smsService;
    private readonly RecentNotificationStore _store;

    public TransactionCreatedConsumer(
        ILogger<TransactionCreatedConsumer> logger,
        IFluentEmail fluentEmail,
        ISmsService smsService,
        RecentNotificationStore store)
    {
        _logger = logger;
        _fluentEmail = fluentEmail;
        _smsService = smsService;
        _store = store;
    }

    public async Task Consume(ConsumeContext<TransactionCreated> context)
    {
        var e = context.Message;

        _logger.LogInformation("TransactionCreated: {TransactionId} for user {UserId} — {Type} {Amount} {Currency}",
            e.TransactionId, e.UserId, e.Type, e.Amount, e.Currency);

        var subject = $"Transaction Confirmed: {e.Type} of {e.Amount} {e.Currency}";
        var body = $"A <strong>{e.Type}</strong> transaction of <strong>{e.Amount} {e.Currency}</strong> has been processed.<br/>" +
                   $"Transaction ID: {e.TransactionId}";

        await _fluentEmail
            .To($"{e.UserId}@notifications.local")
            .Subject(subject)
            .Body(body, isHtml: true)
            .SendAsync();

        await _smsService.SendAsync(
            $"+0000-{e.UserId}",
            $"Transaction confirmed: {e.Type} {e.Amount} {e.Currency}. Ref: {e.TransactionId}.");

        _store.Add(e.UserId, "TransactionCreated",
            $"{e.Type} of {e.Amount} {e.Currency} processed. Ref: {e.TransactionId}.");

        _logger.LogInformation("Notifications dispatched for transaction {TransactionId}", e.TransactionId);
    }
}