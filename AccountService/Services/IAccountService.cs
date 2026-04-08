using AccountService.Controllers;

namespace AccountService.Services;

public interface IAccountService
{
    Task<object> GetAccountsAsync(Guid userId);
    Task<object> GetOrganisationAccountsAsync(Guid organisationId);
    Task<object> CreateAccountAsync(Guid userId, string clientType, Guid? organisationId, CreateAccountRequest request);
    Task<object?> GetBalanceAsync(Guid userId, Guid accountId);
}
