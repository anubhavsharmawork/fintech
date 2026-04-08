using TransactionService.Data;

namespace TransactionService.Services;

/// <summary>
/// Background service that continuously processes AML screening requests
/// from the bounded channel. Exceptions are logged with full context
/// (transaction ID, user ID) and never swallowed silently.
/// </summary>
public sealed class AmlScreeningWorker : BackgroundService
{
    private readonly IAmlScreeningChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AmlScreeningWorker> _logger;

    public AmlScreeningWorker(
        IAmlScreeningChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<AmlScreeningWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AML screening worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            Transaction transaction;
            try
            {
                transaction = await _channel.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await ProcessTransactionAsync(transaction, stoppingToken);
        }

        _logger.LogInformation("AML screening worker stopped");
    }

    private async Task ProcessTransactionAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var amlService = scope.ServiceProvider.GetRequiredService<IAmlService>();

            await amlService.ScreenTransactionAsync(transaction);

            _logger.LogDebug(
                "AML screening completed for transaction {TransactionId}, user {UserId}",
                transaction.Id,
                transaction.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "AML screening failed for transaction {TransactionId}, user {UserId}, amount {Amount} {Currency}",
                transaction.Id,
                transaction.UserId,
                transaction.Amount,
                transaction.Currency);
        }
    }
}
