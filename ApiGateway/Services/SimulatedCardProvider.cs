using System.Collections.Concurrent;

namespace ApiGateway.Services;

/// <summary>
/// In-memory card issuing provider for development and demo purposes.
/// At launch, swap this registration for StripeIssuingProvider — no other code changes needed.
/// </summary>
public sealed class SimulatedCardProvider : ICardIssuingProvider
{
    private readonly ConcurrentDictionary<Guid, List<InMemVirtualCard>> _cardsByUser = new();
    private readonly object _lock = new();

    public Task<IReadOnlyList<VirtualCardDto>> ListCardsAsync(Guid userId)
    {
        var cards = GetUserCards(userId);
        IReadOnlyList<VirtualCardDto> result;
        lock (_lock)
        {
            result = cards
                .Where(c => !c.Deleted)
                .OrderByDescending(c => c.CreatedAt)
                .Select(ToDto)
                .ToList();
        }
        return Task.FromResult(result);
    }

    public Task<CardCreateResultDto> CreateCardAsync(Guid userId, string nickname)
    {
        var cardNumber = LuhnGenerator.Generate();
        var cvv = LuhnGenerator.GenerateCvv();
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddYears(2);

        var card = new InMemVirtualCard
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Nickname = nickname.Trim(),
            CardNumber = cardNumber,
            Cvv = cvv,
            ExpiryMonth = expiry.Month,
            ExpiryYear = expiry.Year,
            Status = "active",
            CreatedAt = now,
            Deleted = false
        };

        var cards = GetUserCards(userId);
        lock (_lock) { cards.Add(card); }

        var dto = ToDto(card);
        var result = new CardCreateResultDto(dto, cardNumber, cvv);
        return Task.FromResult(result);
    }

    public Task<VirtualCardDto> FreezeCardAsync(Guid userId, Guid cardId)
    {
        var card = FindCard(userId, cardId);
        lock (_lock) { card.Status = "frozen"; }
        return Task.FromResult(ToDto(card));
    }

    public Task<VirtualCardDto> UnfreezeCardAsync(Guid userId, Guid cardId)
    {
        var card = FindCard(userId, cardId);
        lock (_lock) { card.Status = "active"; }
        return Task.FromResult(ToDto(card));
    }

    public Task DeleteCardAsync(Guid userId, Guid cardId)
    {
        var card = FindCard(userId, cardId);
        lock (_lock) { card.Deleted = true; }
        return Task.CompletedTask;
    }

    private List<InMemVirtualCard> GetUserCards(Guid userId) =>
        _cardsByUser.GetOrAdd(userId, _ => new List<InMemVirtualCard>());

    private InMemVirtualCard FindCard(Guid userId, Guid cardId)
    {
        var cards = GetUserCards(userId);
        InMemVirtualCard? card;
        lock (_lock) { card = cards.FirstOrDefault(c => c.Id == cardId && !c.Deleted); }
        if (card is null)
            throw new KeyNotFoundException($"Card {cardId} not found for user {userId}.");
        return card;
    }

    private static VirtualCardDto ToDto(InMemVirtualCard c) =>
        new(c.Id, c.UserId, c.Nickname, c.CardNumber[^4..],
            c.ExpiryMonth, c.ExpiryYear, c.Status, c.CreatedAt);
}

/// <summary>
/// Internal storage model — never exposed outside the provider.
/// </summary>
internal sealed class InMemVirtualCard
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string Cvv { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
    public bool Deleted { get; set; }
}
