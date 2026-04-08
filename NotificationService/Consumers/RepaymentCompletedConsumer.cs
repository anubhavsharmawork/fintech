using Contracts.Events;
using FluentEmail.Core;
using MassTransit;
using NotificationService.Stores;

namespace NotificationService.Consumers;

public class RepaymentCompletedConsumer : IConsumer<RepaymentCompleted>
{
    private readonly ILogger<RepaymentCompletedConsumer> _logger;
    private readonly IFluentEmail _fluentEmail;
    private readonly RecentNotificationStore _store;

    public RepaymentCompletedConsumer(
        ILogger<RepaymentCompletedConsumer> logger,
        IFluentEmail fluentEmail,
        RecentNotificationStore store)
    {
        _logger = logger;
        _fluentEmail = fluentEmail;
        _store = store;
    }

    public async Task Consume(ConsumeContext<RepaymentCompleted> context)
    {
        var e = context.Message;
        _logger.LogInformation(
            "RepaymentCompleted: RepaymentId={RepaymentId} UserId={UserId} Amount={Amount} {Currency} OutstandingBalance={Balance}",
            e.RepaymentId, e.UserId, e.Amount, e.Currency, e.OutstandingBalance);

        var subject = "Repayment Confirmed";
        var body = $"Your repayment of <strong>{e.Amount} {e.Currency}</strong> has been completed.<br/>" +
                   $"Outstanding balance: {e.OutstandingBalance} {e.Currency}<br/>" +
                   $"Status: {e.Status}<br/>" +
                   $"Completed at: {e.CompletedAt:u}";

        await _fluentEmail
            .To($"{e.UserId}@notifications.local")
            .Subject(subject)
            .Body(body, isHtml: true)
            .SendAsync();

        _store.Add(e.UserId, "RepaymentCompleted",
            $"Repayment of {e.Amount} {e.Currency} completed. Outstanding balance: {e.OutstandingBalance} {e.Currency}.");
    }
}
