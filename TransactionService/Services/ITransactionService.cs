using TransactionService.Models.Dtos;

namespace TransactionService.Services;

public interface ITransactionService
{
    Task<object> GetTransactionsAsync(
        Guid userId,
        Guid? accountId,
        Guid? batchId,
        int page,
        int pageSize,
        TransactionFilterDto? filter = null);

    Task<object> GetOrganisationTransactionsAsync(Guid organisationId);
    Task<object> CreateTransactionAsync(Guid userId, string clientType, Guid? organisationId, CreatePaymentRequestDto request);
}
