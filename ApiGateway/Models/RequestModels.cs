namespace ApiGateway.Models;

internal record LoginRequest(string Email, string Password);
internal record RegisterRequest(string Email, string Password, string FirstName, string LastName, string? ClientType = "Individual", string? CompanyName = null, string? RegistrationNumber = null);
internal record CreateTransactionRequest(Guid AccountId, decimal Amount, string? Currency, string Type, string? Description, string? IdempotencyKey = null);
internal record CreateAccountRequest(string? AccountType, string? Currency);
internal record DepositFromExternalRequest(Guid ExternalBankAccountId, decimal Amount);
internal record ConnectBankRequest(string BankId);
internal record CreatePayeeRequest(string? Name, string? AccountNumber);
internal record CreatePaymentRequest(Guid AccountId, decimal Amount, string? PayeeName, string? PayeeAccountNumber, string? Description);
internal record TransactionResponse(Guid Id, Guid AccountId, decimal Amount, string Currency, string Type, string Description, DateTime CreatedAt);
internal record SeedStatus(long AccountsCount, long TransactionsCount, bool DemoTransactionsPresent, List<string> Migrations);

// Corporate banking inline request models
internal record InviteMemberInlineRequest(string Email, string Role);
internal record CreateBatchInlineRequest(string? Currency, List<CreateBatchItemInline> Items);
internal record CreateBatchItemInline(Guid SourceAccountId, string PayeeName, string? PayeeAccountNumber, decimal Amount, string? Description);
internal record ApprovalDecisionInlineRequest(string Decision, string? Comments);

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)Math.Round(TemperatureC * 9.0 / 5.0);
}
