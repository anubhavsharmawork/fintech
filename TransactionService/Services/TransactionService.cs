using Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using TransactionService.Controllers;
using TransactionService.Constants;
using TransactionService.Data;
using TransactionService.Models.Dtos;

namespace TransactionService.Services;

public class TransactionService : ITransactionService
{
    private readonly TransactionDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IAmlScreeningChannel _amlChannel;
    private readonly ICacheService _cacheService;
    private readonly ILogger<TransactionService> _logger;

    private const int TransactionsCacheTtlMinutes = 2;
    private const int DefaultPageSize = 50;

    public TransactionService(
        TransactionDbContext context,
        IPublishEndpoint publishEndpoint,
        IAmlScreeningChannel amlChannel,
        ICacheService cacheService,
        ILogger<TransactionService> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _amlChannel = amlChannel;
        _cacheService = cacheService;
        _logger = logger;
    }

    private static string GetUserTransactionsCacheKey(Guid userId, int page) => $"user:{userId}:page:{page}";

    public async Task<object> GetTransactionsAsync(
        Guid userId,
        Guid? accountId,
        Guid? batchId,
        int page,
        int pageSize,
        TransactionFilterDto? filter = null)
    {
        // Skip caching when filters are applied to ensure accurate results
        var hasFilters = filter is not null &&
            (filter.FromDate.HasValue || filter.ToDate.HasValue ||
             !string.IsNullOrWhiteSpace(filter.TransactionType) ||
             filter.MinAmount.HasValue || filter.MaxAmount.HasValue ||
             !string.IsNullOrWhiteSpace(filter.SearchTerm));

        var canCache = !accountId.HasValue && !batchId.HasValue && !hasFilters;

        if (canCache)
        {
            var cacheKey = GetUserTransactionsCacheKey(userId, page);
            var cached = await _cacheService.GetAsync<List<TransactionDto>>(cacheKey);
            if (cached is not null)
            {
                return new TransactionPagedResponse<object>
                {
                    Data = cached.Select(t => new {
                        id = t.Id,
                        accountId = t.AccountId,
                        amount = t.Amount,
                        currency = t.Currency,
                        type = t.Type,
                        description = t.Description,
                        spendingType = t.SpendingType,
                        txHash = t.TxHash,
                        createdAt = t.CreatedAt,
                        clientType = t.ClientType,
                        organisationId = t.OrganisationId,
                        paymentBatchId = t.PaymentBatchId,
                        status = t.Status
                    }),
                    Page = page,
                    PageSize = pageSize,
                    TotalFilteredCount = cached.Count
                };
            }
        }

        var query = _context.Transactions.Where(t => t.UserId == userId);

        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId.Value);

        if (batchId.HasValue)
            query = query.Where(t => t.PaymentBatchId == batchId.Value);

        // Apply composable filters
        if (filter is not null)
        {
            if (filter.FromDate.HasValue)
                query = query.Where(t => t.CreatedAt >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
            {
                // Include the entire end date (up to 23:59:59.999)
                var endOfDay = filter.ToDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(t => t.CreatedAt <= endOfDay);
            }

            if (!string.IsNullOrWhiteSpace(filter.TransactionType))
                query = query.Where(t => t.Type.ToLower() == filter.TransactionType.ToLower());

            if (filter.MinAmount.HasValue)
                query = query.Where(t => t.Amount >= filter.MinAmount.Value);

            if (filter.MaxAmount.HasValue)
                query = query.Where(t => t.Amount <= filter.MaxAmount.Value);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                query = query.Where(t => t.Description.ToLower().Contains(filter.SearchTerm.ToLower()));
        }

        // Get total count before pagination
        var totalFilteredCount = await query.CountAsync();

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = transactions.Select(t => new TransactionDto(
            t.Id,
            t.AccountId,
            t.Amount,
            t.Currency,
            t.Type,
            t.Description,
            t.SpendingType,
            t.TxHash,
            t.CreatedAt,
            t.ClientType,
            t.OrganisationId,
            t.PaymentBatchId,
            t.Status
        )).ToList();

        if (canCache)
        {
            var cacheKey = GetUserTransactionsCacheKey(userId, page);
            await _cacheService.SetAsync(cacheKey, dtos, TransactionsCacheTtlMinutes);
        }

        return new TransactionPagedResponse<object>
        {
            Data = dtos.Select(t => new {
                id = t.Id,
                accountId = t.AccountId,
                amount = t.Amount,
                currency = t.Currency,
                type = t.Type,
                description = t.Description,
                spendingType = t.SpendingType,
                txHash = t.TxHash,
                createdAt = t.CreatedAt,
                clientType = t.ClientType,
                organisationId = t.OrganisationId,
                paymentBatchId = t.PaymentBatchId,
                status = t.Status
            }),
            Page = page,
            PageSize = pageSize,
            TotalFilteredCount = totalFilteredCount
        };
    }

    public async Task<object> GetOrganisationTransactionsAsync(Guid organisationId)
    {
        var transactions = await _context.Transactions
            .Where(t => t.OrganisationId == organisationId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return transactions.Select(t => new {
            id = t.Id,
            accountId = t.AccountId,
            amount = t.Amount,
            currency = t.Currency,
            type = t.Type,
            description = t.Description,
            spendingType = t.SpendingType,
            txHash = t.TxHash,
            createdAt = t.CreatedAt,
            clientType = t.ClientType,
            organisationId = t.OrganisationId,
            paymentBatchId = t.PaymentBatchId,
            status = t.Status
        });
    }

    public async Task<object> CreateTransactionAsync(Guid userId, string clientType, Guid? organisationId, CreatePaymentRequestDto request)
    {
        var spendingType = request.SpendingType ?? "Fun";
        if (!SpendingTypeConstants.IsValid(spendingType))
        {
            throw new ArgumentException("Invalid spendingType. Allowed: Fun, Fixed, Future");
        }

        var normalizedSpendingType = SpendingTypeConstants.Normalize(spendingType);
        var txHash = string.IsNullOrWhiteSpace(request.TxHash) ? null : request.TxHash.Trim();
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "NZD" : request.Currency.Trim().ToUpperInvariant();
        var type = string.IsNullOrWhiteSpace(request.Type) ? "debit" : request.Type.Trim();
        var description = request.Description?.Trim() ?? string.Empty;

        var transactionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var transaction = new Transaction
        {
            Id = transactionId,
            AccountId = request.AccountId,
            UserId = userId,
            Amount = request.Amount,
            Currency = currency,
            Type = type,
            Description = description,
            SpendingType = normalizedSpendingType,
            TxHash = txHash,
            ClientType = clientType,
            OrganisationId = organisationId,
            CreatedAt = now,
            Status = "Completed"
        };

        // Double-entry bookkeeping: create debit and credit ledger entries atomically
        var debitEntry = new Models.LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            AccountId = request.AccountId, // Source account (debit)
            EntryType = "debit",
            Amount = request.Amount,
            Currency = currency,
            CreatedAt = now
        };

        var creditEntry = new Models.LedgerEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            AccountId = request.DestinationAccountId ?? request.AccountId, // Destination account (credit) - falls back to same account for single-leg transactions
            EntryType = "credit",
            Amount = request.Amount,
            Currency = currency,
            CreatedAt = now
        };

        // Use a transaction to ensure atomicity of double-entry
        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.Transactions.Add(transaction);
            _context.LedgerEntries.Add(debitEntry);
            _context.LedgerEntries.Add(creditEntry);
            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }

        await _cacheService.RemoveAsync(GetUserTransactionsCacheKey(userId, 1));

        var clientTypeEnum = string.Equals(clientType, "Corporate", StringComparison.OrdinalIgnoreCase)
            ? Contracts.Events.ClientType.Corporate
            : Contracts.Events.ClientType.Individual;

        var transactionCreated = new TransactionCreated(
            transaction.Id,
            transaction.AccountId,
            transaction.UserId,
            transaction.Amount,
            transaction.Currency,
            transaction.Type,
            transaction.CreatedAt,
            clientTypeEnum,
            organisationId
        );

        await _publishEndpoint.Publish(transactionCreated);

        if (!_amlChannel.TryEnqueue(transaction))
        {
            _logger.LogWarning(
                "AML screening channel full — transaction {TransactionId} for user {UserId} was not queued for screening",
                transaction.Id,
                transaction.UserId);
        }

        _logger.LogInformation("Transaction created: {TransactionId} for account {AccountId} with double-entry ledger", 
            transaction.Id, transaction.AccountId);

        return new {
            id = transaction.Id,
            accountId = transaction.AccountId,
            amount = transaction.Amount,
            currency = transaction.Currency,
            type = transaction.Type,
            description = transaction.Description,
            spendingType = transaction.SpendingType,
            txHash = transaction.TxHash,
            createdAt = transaction.CreatedAt,
            clientType = transaction.ClientType,
            organisationId = transaction.OrganisationId,
            paymentBatchId = transaction.PaymentBatchId,
            status = transaction.Status
        };
    }
}
