using Contracts.Events;
using FluentEmail.Core;
using MassTransit;
using NotificationService.Services;
using NotificationService.Stores;

namespace NotificationService.Consumers;

public class SuspiciousActivityFlaggedConsumer : IConsumer<SuspiciousActivityFlagged>
{
    private readonly ILogger<SuspiciousActivityFlaggedConsumer> _logger;
    private readonly IFluentEmail _fluentEmail;
    private readonly ISmsService _smsService;
    private readonly RecentNotificationStore _store;

    public SuspiciousActivityFlaggedConsumer(
        ILogger<SuspiciousActivityFlaggedConsumer> logger,
        IFluentEmail fluentEmail,
        ISmsService smsService,
        RecentNotificationStore store)
    {
        _logger = logger;
        _fluentEmail = fluentEmail;
        _smsService = smsService;
        _store = store;
    }

    public async Task Consume(ConsumeContext<SuspiciousActivityFlagged> context)
    {
        var e = context.Message;
        _logger.LogWarning(
            "SuspiciousActivityFlagged: Id={Id} UserId={UserId} RiskLevel={RiskLevel} Reason={Reason}",
            e.Id, e.UserId, e.RiskLevel, e.Reason);

        var subject = $"Security Alert: Suspicious Activity Detected ({e.RiskLevel})";
        var body = $"Suspicious activity was flagged on your account.<br/>" +
                   $"Amount: {e.Amount} {e.Currency}<br/>" +
                   $"Reason: {e.Reason}<br/>" +
                   $"Risk Level: <strong>{e.RiskLevel}</strong><br/>" +
                   $"Flagged At: {e.FlaggedAt:u}";

        await _fluentEmail
            .To($"{e.UserId}@notifications.local")
            .Subject(subject)
            .Body(body, isHtml: true)
            .SendAsync();

        await _smsService.SendAsync(
            $"+0000-{e.UserId}",
            $"[ALERT] Suspicious activity flagged on your account. Risk: {e.RiskLevel}. Amount: {e.Amount} {e.Currency}.");

        _store.Add(e.UserId, "SuspiciousActivityFlagged",
            $"Suspicious activity flagged. Risk: {e.RiskLevel}. Reason: {e.Reason}. Amount: {e.Amount} {e.Currency}.");
    }
}
