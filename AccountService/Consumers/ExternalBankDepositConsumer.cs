using MassTransit;
using Contracts.Events;
using AccountService.Data;
using Microsoft.EntityFrameworkCore;

namespace AccountService.Consumers;

public class ExternalBankDepositConsumer : IConsumer<ExternalBankDepositInitiated>
{
    private readonly ILogger<ExternalBankDepositConsumer> _logger;
    private readonly AccountDbContext _context;

    public ExternalBankDepositConsumer(ILogger<ExternalBankDepositConsumer> logger, AccountDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task Consume(ConsumeContext<ExternalBankDepositInitiated> context)
    {
        var deposit = context.Message;

        _logger.LogInformation(
            "Processing external bank deposit {DepositId}: {Amount} {Currency} into account {AccountId} for user {UserId}",
            deposit.DepositId, deposit.Amount, deposit.Currency, deposit.AccountId, deposit.UserId);

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == deposit.AccountId && a.UserId == deposit.UserId);

        if (account is null)
        {
            _logger.LogWarning(
                "Account {AccountId} not found for user {UserId} during deposit {DepositId}",
                deposit.AccountId, deposit.UserId, deposit.DepositId);
            return;
        }

        account.Balance += deposit.Amount;
        account.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict processing deposit {DepositId} for account {AccountId}. The balance was modified by another request.",
                deposit.DepositId, deposit.AccountId);
            throw; // Let MassTransit retry the message
        }

        _logger.LogInformation(
            "External bank deposit {DepositId} completed: {Amount} {Currency} credited to account {AccountId}. New balance: {Balance}",
            deposit.DepositId, deposit.Amount, deposit.Currency, deposit.AccountId, account.Balance);
    }
}
