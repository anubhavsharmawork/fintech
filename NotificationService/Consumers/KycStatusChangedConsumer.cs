using Contracts.Events;
using FluentEmail.Core;
using MassTransit;
using NotificationService.Stores;

namespace NotificationService.Consumers;

public class KycStatusChangedConsumer : IConsumer<KycStatusChanged>
{
    private readonly ILogger<KycStatusChangedConsumer> _logger;
    private readonly IFluentEmail _fluentEmail;
    private readonly RecentNotificationStore _store;

    public KycStatusChangedConsumer(
        ILogger<KycStatusChangedConsumer> logger,
        IFluentEmail fluentEmail,
        RecentNotificationStore store)
    {
        _logger = logger;
        _fluentEmail = fluentEmail;
        _store = store;
    }

    public async Task Consume(ConsumeContext<KycStatusChanged> context)
    {
        var e = context.Message;
        _logger.LogInformation("KycStatusChanged: UserId={UserId} Status={Status}", e.UserId, e.Status);

        var subject = $"KYC Status Update: {e.Status}";
        var body = $"Your KYC status has been updated to <strong>{e.Status}</strong>. Notes: {e.Notes}";

        await _fluentEmail
            .To($"{e.UserId}@notifications.local")
            .Subject(subject)
            .Body(body, isHtml: true)
            .SendAsync();

        _store.Add(e.UserId, "KycStatusChanged", $"KYC status changed to {e.Status}. {e.Notes}");
    }
}
