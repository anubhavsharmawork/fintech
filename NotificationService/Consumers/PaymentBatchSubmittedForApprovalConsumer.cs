using Contracts.Events;
using FluentEmail.Core;
using MassTransit;
using NotificationService.Stores;

namespace NotificationService.Consumers;

public class PaymentBatchSubmittedForApprovalConsumer : IConsumer<PaymentBatchSubmittedForApproval>
{
    private readonly ILogger<PaymentBatchSubmittedForApprovalConsumer> _logger;
    private readonly IFluentEmail _fluentEmail;
    private readonly RecentNotificationStore _store;

    public PaymentBatchSubmittedForApprovalConsumer(
        ILogger<PaymentBatchSubmittedForApprovalConsumer> logger,
        IFluentEmail fluentEmail,
        RecentNotificationStore store)
    {
        _logger = logger;
        _fluentEmail = fluentEmail;
        _store = store;
    }

    public async Task Consume(ConsumeContext<PaymentBatchSubmittedForApproval> context)
    {
        var e = context.Message;
        _logger.LogInformation(
            "PaymentBatchSubmittedForApproval: BatchId={BatchId} SubmittedBy={SubmittedBy} Items={Items} Total={Total} {Currency}",
            e.BatchId, e.SubmittedByUserId, e.ItemCount, e.TotalAmount, e.Currency);

        var subject = "Payment Batch Submitted for Approval";
        var body = $"Payment batch <strong>{e.BatchId}</strong> has been submitted for approval.<br/>" +
                   $"Items: {e.ItemCount} | Total: {e.TotalAmount} {e.Currency}<br/>" +
                   $"Submitted at: {e.SubmittedAt:u}";

        await _fluentEmail
            .To($"{e.SubmittedByUserId}@notifications.local")
            .Subject(subject)
            .Body(body, isHtml: true)
            .SendAsync();

        _store.Add(e.SubmittedByUserId, "PaymentBatchSubmittedForApproval",
            $"Payment batch {e.BatchId} submitted for approval. {e.ItemCount} items, {e.TotalAmount} {e.Currency}.");
    }
}
