using Contracts.Events;
using FluentEmail.Core;
using MassTransit;
using NotificationService.Stores;

namespace NotificationService.Consumers;

public class PaymentApprovedConsumer : IConsumer<PaymentApproved>
{
    private readonly ILogger<PaymentApprovedConsumer> _logger;
    private readonly IFluentEmail _fluentEmail;
    private readonly RecentNotificationStore _store;

    public PaymentApprovedConsumer(
        ILogger<PaymentApprovedConsumer> logger,
        IFluentEmail fluentEmail,
        RecentNotificationStore store)
    {
        _logger = logger;
        _fluentEmail = fluentEmail;
        _store = store;
    }

    public async Task Consume(ConsumeContext<PaymentApproved> context)
    {
        var e = context.Message;
        _logger.LogInformation(
            "PaymentApproved: BatchId={BatchId} OrganisationId={OrganisationId} ApprovedBy={ApprovedBy}",
            e.BatchId, e.OrganisationId, e.ApprovedByUserId);

        var subject = "Payment Batch Approved";
        var body = $"Payment batch <strong>{e.BatchId}</strong> has been approved by user {e.ApprovedByUserId} at {e.ApprovedAt:u}.";

        await _fluentEmail
            .To($"{e.ApprovedByUserId}@notifications.local")
            .Subject(subject)
            .Body(body, isHtml: true)
            .SendAsync();

        _store.Add(e.ApprovedByUserId, "PaymentApproved",
            $"Payment batch {e.BatchId} approved at {e.ApprovedAt:u}.");
    }
}
