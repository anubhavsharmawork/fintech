using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransactionService.Configuration;
using TransactionService.Data;
using TransactionService.Models;
using Contracts.Events;

namespace TransactionService.Services;

public interface IAmlService
{
    Task<bool> ScreenTransactionAsync(Transaction tx);
    Task<SuspiciousActivityReport?> GetReportByTransactionAsync(Guid transactionId);
}

public class AmlService : IAmlService
{
    private readonly FraudDetectionSettings _settings;
    private readonly TransactionDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<AmlService> _logger;

    public AmlService(
        IOptions<FraudDetectionSettings> settings,
        TransactionDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<AmlService> logger)
    {
        _settings = settings.Value;
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<bool> ScreenTransactionAsync(Transaction tx)
    {
        string? reason = null;
        string? riskLevel = null;

        // Rule 1: Large transaction threshold
        if (tx.Amount > _settings.LargeTransactionThreshold)
        {
            reason = "Large transaction threshold exceeded";
            riskLevel = "High";
        }
        // Rule 2: Rapid successive debit transactions
        else if (tx.Amount > _settings.RapidDebitThreshold
                 && string.Equals(tx.Type, "debit", StringComparison.OrdinalIgnoreCase))
        {
            var windowStart = DateTime.UtcNow.AddMinutes(-_settings.RapidTransactionWindowMinutes);
            var recentCount = await _context.Transactions
                .CountAsync(t => t.UserId == tx.UserId && t.CreatedAt >= windowStart);

            if (recentCount >= _settings.RapidTransactionCount)
            {
                reason = "Rapid successive debit transactions";
                riskLevel = "Medium";
            }
        }

        // Rule 3: Flagged keywords in description
        if (reason is null && !string.IsNullOrWhiteSpace(tx.Description))
        {
            var descLower = tx.Description.ToLowerInvariant();
            foreach (var keyword in _settings.FlaggedKeywords)
            {
                if (descLower.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "Flagged keyword in description";
                    riskLevel = "Low";
                    break;
                }
            }
        }

        // Rule 4: Unusually large bulk corporate payments
        if (reason is null
            && string.Equals(tx.ClientType, "Corporate", StringComparison.OrdinalIgnoreCase)
            && tx.OrganisationId.HasValue)
        {
            var windowStart = DateTime.UtcNow.AddHours(-24);
            var recentCorporateTotal = await _context.Transactions
                .Where(t => t.OrganisationId == tx.OrganisationId
                         && t.CreatedAt >= windowStart)
                .SumAsync(t => t.Amount);

            if (recentCorporateTotal > _settings.LargeTransactionThreshold * 10)
            {
                reason = "Unusually large bulk corporate payment volume in 24h window";
                riskLevel = "High";
            }
        }

        if (reason is null || riskLevel is null)
            return false;

        var report = new SuspiciousActivityReport
        {
            Id = Guid.NewGuid(),
            TransactionId = tx.Id,
            UserId = tx.UserId,
            AccountId = tx.AccountId,
            Amount = tx.Amount,
            Currency = tx.Currency,
            Reason = reason,
            RiskLevel = riskLevel,
            Status = "Open",
            FlaggedAt = DateTime.UtcNow
        };

        _context.SuspiciousActivityReports.Add(report);
        await _context.SaveChangesAsync();

        await _publishEndpoint.Publish(new SuspiciousActivityFlagged(
            report.Id,
            report.TransactionId,
            report.UserId,
            report.AccountId,
            report.Amount,
            report.Currency,
            report.Reason,
            report.RiskLevel,
            report.FlaggedAt,
            string.Equals(tx.ClientType, "Corporate", StringComparison.OrdinalIgnoreCase)
                ? Contracts.Events.ClientType.Corporate
                : Contracts.Events.ClientType.Individual,
            tx.OrganisationId
        ));

        _logger.LogInformation(
            "SAR created: {SarId} for transaction {TransactionId}, RiskLevel={RiskLevel}, Reason={Reason}",
            report.Id, report.TransactionId, report.RiskLevel, report.Reason);

        return true;
    }

    public async Task<SuspiciousActivityReport?> GetReportByTransactionAsync(Guid transactionId)
    {
        return await _context.SuspiciousActivityReports
            .FirstOrDefaultAsync(r => r.TransactionId == transactionId);
    }
}
