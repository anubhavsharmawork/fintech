namespace ApiGateway.Services;

public record VirtualCardDto(
    Guid Id,
    Guid UserId,
    string Nickname,
    string Last4,
    int ExpiryMonth,
    int ExpiryYear,
    string Status,
    DateTimeOffset CreatedAt);

public record CardCreateResultDto(
    VirtualCardDto Card,
    string CardNumber,
    string Cvv);

public interface ICardIssuingProvider
{
    Task<IReadOnlyList<VirtualCardDto>> ListCardsAsync(Guid userId);
    Task<CardCreateResultDto> CreateCardAsync(Guid userId, string nickname);
    Task<VirtualCardDto> FreezeCardAsync(Guid userId, Guid cardId);
    Task<VirtualCardDto> UnfreezeCardAsync(Guid userId, Guid cardId);
    Task DeleteCardAsync(Guid userId, Guid cardId);
}
