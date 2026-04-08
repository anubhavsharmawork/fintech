using ApiGateway.Services;

namespace Tests;

public class LuhnGeneratorTests
{
    [Fact]
    public void Generate_Returns_16_Digit_String()
    {
        var number = LuhnGenerator.Generate();
        number.Should().HaveLength(16);
        number.Should().MatchRegex(@"^\d{16}$");
    }

    [Fact]
    public void Generate_Passes_Luhn_Validation()
    {
        for (var i = 0; i < 50; i++)
        {
            var number = LuhnGenerator.Generate();
            LuhnGenerator.IsValid(number).Should().BeTrue($"Generated number {number} should be Luhn-valid");
        }
    }

    [Fact]
    public void Generate_Uses_Supplied_BinPrefix()
    {
        var number = LuhnGenerator.Generate("5234");
        number.Should().StartWith("5234");
        LuhnGenerator.IsValid(number).Should().BeTrue();
    }

    [Fact]
    public void IsValid_Rejects_Altered_Number()
    {
        var number = LuhnGenerator.Generate();
        // Flip one digit
        var altered = number[..^2] + ((number[^2] - '0' + 1) % 10) + number[^1..];
        LuhnGenerator.IsValid(altered).Should().BeFalse();
    }

    [Fact]
    public void IsValid_Rejects_Null_And_Short()
    {
        LuhnGenerator.IsValid(null!).Should().BeFalse();
        LuhnGenerator.IsValid("").Should().BeFalse();
        LuhnGenerator.IsValid("1").Should().BeFalse();
    }

    [Fact]
    public void GenerateCvv_Returns_3_Digit_String()
    {
        for (var i = 0; i < 20; i++)
        {
            var cvv = LuhnGenerator.GenerateCvv();
            cvv.Should().HaveLength(3);
            cvv.Should().MatchRegex(@"^\d{3}$");
        }
    }
}

public class SimulatedCardProviderTests
{
    private readonly SimulatedCardProvider _provider = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task ListCards_Returns_Empty_For_New_User()
    {
        var cards = await _provider.ListCardsAsync(_userId);
        cards.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateCard_Returns_Full_Number_And_Cvv()
    {
        var result = await _provider.CreateCardAsync(_userId, "Test Card");

        result.Card.Should().NotBeNull();
        result.Card.Nickname.Should().Be("Test Card");
        result.Card.Status.Should().Be("active");
        result.Card.Last4.Should().HaveLength(4);
        result.CardNumber.Should().HaveLength(16);
        result.CardNumber.Should().EndWith(result.Card.Last4);
        result.Cvv.Should().HaveLength(3);
        LuhnGenerator.IsValid(result.CardNumber).Should().BeTrue();
    }

    [Fact]
    public async Task CreateCard_Appears_In_List()
    {
        await _provider.CreateCardAsync(_userId, "Card A");
        await _provider.CreateCardAsync(_userId, "Card B");

        var cards = await _provider.ListCardsAsync(_userId);
        cards.Should().HaveCount(2);
        cards.Select(c => c.Nickname).Should().Contain("Card A").And.Contain("Card B");
    }

    [Fact]
    public async Task ListCards_Returns_Only_Last4_Not_Full_Number()
    {
        var result = await _provider.CreateCardAsync(_userId, "Secure");
        var cards = await _provider.ListCardsAsync(_userId);

        cards.First().Last4.Should().HaveLength(4);
        cards.First().Last4.Should().Be(result.CardNumber[^4..]);
    }

    [Fact]
    public async Task FreezeCard_Sets_Status_To_Frozen()
    {
        var result = await _provider.CreateCardAsync(_userId, "Freezable");
        var frozen = await _provider.FreezeCardAsync(_userId, result.Card.Id);

        frozen.Status.Should().Be("frozen");
    }

    [Fact]
    public async Task UnfreezeCard_Sets_Status_To_Active()
    {
        var result = await _provider.CreateCardAsync(_userId, "Thaw");
        await _provider.FreezeCardAsync(_userId, result.Card.Id);
        var active = await _provider.UnfreezeCardAsync(_userId, result.Card.Id);

        active.Status.Should().Be("active");
    }

    [Fact]
    public async Task DeleteCard_Removes_From_List()
    {
        var result = await _provider.CreateCardAsync(_userId, "Gone");
        await _provider.DeleteCardAsync(_userId, result.Card.Id);

        var cards = await _provider.ListCardsAsync(_userId);
        cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Operations_On_Missing_Card_Throw_KeyNotFoundException()
    {
        var bogusId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _provider.FreezeCardAsync(_userId, bogusId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _provider.UnfreezeCardAsync(_userId, bogusId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _provider.DeleteCardAsync(_userId, bogusId));
    }

    [Fact]
    public async Task Cards_Are_Isolated_Per_User()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await _provider.CreateCardAsync(userA, "A's card");
        await _provider.CreateCardAsync(userB, "B's card");

        var aCards = await _provider.ListCardsAsync(userA);
        var bCards = await _provider.ListCardsAsync(userB);

        aCards.Should().HaveCount(1);
        aCards.First().Nickname.Should().Be("A's card");
        bCards.Should().HaveCount(1);
        bCards.First().Nickname.Should().Be("B's card");
    }

    [Fact]
    public async Task User_Cannot_Operate_On_Another_Users_Card()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var result = await _provider.CreateCardAsync(userA, "A only");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _provider.FreezeCardAsync(userB, result.Card.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _provider.DeleteCardAsync(userB, result.Card.Id));
    }
}
