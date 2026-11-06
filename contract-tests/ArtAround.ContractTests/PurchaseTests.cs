using System.Net;
using System.Text.Json;
using static ArtAround.ContractTests.TestData;

namespace ArtAround.ContractTests;

/*
 * POST  /api/items/:id/purchase - Buy an item (auth)
 * PATCH /api/auth/wallet        - Recharge the caller's wallet (auth)
 * POST  /api/visits             - Also buys the other authors' items the visit uses
 *
 * Every test registers its own users, because each one checks exact wallet balances.
 * New users start with 100 credits.
 */

/// <summary>The content and museum new items and visits attach to.</summary>
public class SeededContentFixture : IAsyncLifetime
{
    public string MuseumId { get; private set; } = "";
    public string ContentId { get; private set; } = "";

    public async Task InitializeAsync()
    {
        using var museums = await Api.SendAsync(HttpMethod.Get, "/api/museums");
        MuseumId = (await Api.ReadJsonAsync(museums)).GetProperty("museums")[0].GetProperty("_id").GetString()!;

        using var contents = await Api.SendAsync(HttpMethod.Get, $"/api/museums/{MuseumId}/contents");
        ContentId = (await Api.ReadJsonAsync(contents)).GetProperty("contents")[0].GetProperty("universalId").GetString()!;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class PurchaseTests(SeededContentFixture fixture) : IClassFixture<SeededContentFixture>
{
    /* ---------- helpers ---------- */

    private static Task<string> NewUserAsync() => Api.RegisterAndGetTokenAsync(UniqueUsername(12));

    private async Task<string> CreateItemAsync(string token, int price, bool isPublic = true)
    {
        using var response = await Api.SendAsync(HttpMethod.Post, "/api/items", token, new
        {
            contentId = fixture.ContentId,
            targetAudience = "general",
            price,
            isPublic,
            descriptions = new[] { new { tone = "easy", texts = new[] { new { text = "Text", lengthCategory = "30s" } } } },
            texts = new[] { new { tone = "easy", text = "Text" } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Api.ReadJsonAsync(response)).GetProperty("item").GetProperty("_id").GetString()!;
    }

    private static Task<HttpResponseMessage> PurchaseAsync(string token, string itemId) =>
        Api.SendAsync(HttpMethod.Post, $"/api/items/{itemId}/purchase", token);

    private static async Task<int> BalanceAsync(string token)
    {
        using var response = await Api.SendAsync(HttpMethod.Get, "/api/auth/me", token);
        return (await Api.ReadJsonAsync(response)).GetProperty("user").GetProperty("walletBalance").GetInt32();
    }

    private object VisitBody(params string[] itemIds) => new
    {
        title = "Paid visit",
        museumId = fixture.MuseumId,
        sequence = itemIds.Select(itemId => new { itemId }).ToArray(),
    };

    /* ---------- purchase ---------- */

    [Fact]
    public async Task Purchase_MovesCreditsFromBuyerToCreator()
    {
        var seller = await NewUserAsync();
        var buyer = await NewUserAsync();
        var itemId = await CreateItemAsync(seller, price: 30);

        using var response = await PurchaseAsync(buyer, itemId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Api.ReadJsonAsync(response);
        Assert.Equal(30, body.GetProperty("chargedAmount").GetInt32());
        Assert.Equal(70, body.GetProperty("walletBalance").GetInt32());
        Assert.Equal(itemId, body.GetProperty("adoptedItemIds")[0].GetString());
        Assert.Equal(70, await BalanceAsync(buyer));
        Assert.Equal(130, await BalanceAsync(seller));
    }

    [Fact]
    public async Task Purchase_ShowsOnTheItemForBuyerAndCreator()
    {
        var seller = await NewUserAsync();
        var buyer = await NewUserAsync();
        var itemId = await CreateItemAsync(seller, price: 30);
        using var purchase = await PurchaseAsync(buyer, itemId);

        using var asBuyer = await Api.SendAsync(HttpMethod.Get, $"/api/items/{itemId}", buyer);
        using var asSeller = await Api.SendAsync(HttpMethod.Get, $"/api/items/{itemId}", seller);

        var buyerView = (await Api.ReadJsonAsync(asBuyer)).GetProperty("item");
        Assert.True(buyerView.GetProperty("isPurchased").GetBoolean());
        Assert.Equal(0, buyerView.GetProperty("adoptionPrice").GetInt32());
        Assert.False(buyerView.TryGetProperty("revenue", out _));
        var sellerView = (await Api.ReadJsonAsync(asSeller)).GetProperty("item");
        Assert.Equal(1, sellerView.GetProperty("salesCount").GetInt32());
        Assert.Equal(30, sellerView.GetProperty("revenue").GetInt32());
    }

    [Fact]
    public async Task Purchase_Twice_ChargesOnce()
    {
        var seller = await NewUserAsync();
        var buyer = await NewUserAsync();
        var itemId = await CreateItemAsync(seller, price: 30);
        using var first = await PurchaseAsync(buyer, itemId);

        using var second = await PurchaseAsync(buyer, itemId);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(0, (await Api.ReadJsonAsync(second)).GetProperty("chargedAmount").GetInt32());
        Assert.Equal(70, await BalanceAsync(buyer));
    }

    [Fact]
    public async Task Purchase_InsufficientBalance_Returns400AndChangesNothing()
    {
        var seller = await NewUserAsync();
        var buyer = await NewUserAsync();
        var itemId = await CreateItemAsync(seller, price: 101);

        using var response = await PurchaseAsync(buyer, itemId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Insufficient balance", (await Api.ReadJsonAsync(response)).GetProperty("error").GetString());
        Assert.Equal(100, await BalanceAsync(buyer));
        Assert.Equal(100, await BalanceAsync(seller));
    }

    [Fact]
    public async Task Purchase_OwnItem_Returns400()
    {
        var seller = await NewUserAsync();
        var itemId = await CreateItemAsync(seller, price: 10);

        using var response = await PurchaseAsync(seller, itemId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Purchase_PrivateItem_Returns403()
    {
        var seller = await NewUserAsync();
        var buyer = await NewUserAsync();
        var itemId = await CreateItemAsync(seller, price: 10, isPublic: false);

        using var response = await PurchaseAsync(buyer, itemId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(100, await BalanceAsync(buyer));
    }

    [Fact]
    public async Task Purchase_Anonymous_Returns401()
    {
        var seller = await NewUserAsync();
        var itemId = await CreateItemAsync(seller, price: 10);

        using var response = await Api.SendAsync(HttpMethod.Post, $"/api/items/{itemId}/purchase");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Purchase_DeletedItem_Returns404()
    {
        var seller = await NewUserAsync();
        var buyer = await NewUserAsync();
        var itemId = await CreateItemAsync(seller, price: 10);
        using var delete = await Api.SendAsync(HttpMethod.Delete, $"/api/items/{itemId}", seller);

        using var response = await PurchaseAsync(buyer, itemId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /* ---------- wallet ---------- */

    [Fact]
    public async Task Recharge_AddsToBalance()
    {
        var token = await NewUserAsync();

        using var response = await Api.SendAsync(HttpMethod.Patch, "/api/auth/wallet", token, new { amount = 50 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(150, (await Api.ReadJsonAsync(response)).GetProperty("walletBalance").GetInt32());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10001)]
    public async Task Recharge_InvalidAmount_Returns400(int amount)
    {
        var token = await NewUserAsync();

        using var response = await Api.SendAsync(HttpMethod.Patch, "/api/auth/wallet", token, new { amount });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(100, await BalanceAsync(token));
    }

    /* ---------- charging on visit save ---------- */

    [Fact]
    public async Task SaveVisit_BuysOtherAuthorsItemsOnce()
    {
        var seller = await NewUserAsync();
        var author = await NewUserAsync();
        var paid = await CreateItemAsync(seller, price: 40);
        var own = await CreateItemAsync(author, price: 5);

        using var created = await Api.SendAsync(HttpMethod.Post, "/api/visits", author, VisitBody(paid, own));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await Api.ReadJsonAsync(created);
        Assert.Equal(40, body.GetProperty("chargedAmount").GetInt32());
        Assert.Equal(60, body.GetProperty("walletBalance").GetInt32());
        var visitId = body.GetProperty("visit").GetProperty("_id").GetString();

        using var saved = await Api.SendAsync(HttpMethod.Put, $"/api/visits/{visitId}", author, VisitBody(paid, own));

        Assert.Equal(0, (await Api.ReadJsonAsync(saved)).GetProperty("chargedAmount").GetInt32());
        Assert.Equal(60, await BalanceAsync(author));
        Assert.Equal(140, await BalanceAsync(seller));
    }

    // The save and the charge are one transaction: no credits leave without a visit, and no visit without paying.
    [Fact]
    public async Task SaveVisit_InsufficientBalance_SavesNothing()
    {
        var seller = await NewUserAsync();
        var author = await NewUserAsync();
        var paid = await CreateItemAsync(seller, price: 101);

        using var created = await Api.SendAsync(HttpMethod.Post, "/api/visits", author, VisitBody(paid));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        using var mine = await Api.SendAsync(HttpMethod.Get, "/api/visits/my", author);
        Assert.Equal(0, (await Api.ReadJsonAsync(mine)).GetProperty("visits").GetArrayLength());
        Assert.Equal(100, await BalanceAsync(author));
    }

    [Fact]
    public async Task SaveVisit_WithOthersPrivateItem_Returns403()
    {
        var seller = await NewUserAsync();
        var author = await NewUserAsync();
        var privateItem = await CreateItemAsync(seller, price: 0, isPublic: false);

        using var created = await Api.SendAsync(HttpMethod.Post, "/api/visits", author, VisitBody(privateItem));

        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
    }

    /* ---------- concurrency ---------- */

    // Two 60-credit items and a balance of 100: without locking, both requests read 100 and both pass.
    [Fact]
    public async Task ConcurrentPurchases_CannotOverspend()
    {
        var seller = await NewUserAsync();
        var buyer = await NewUserAsync();
        var items = new List<string>();
        for (var i = 0; i < 6; i++)
        {
            items.Add(await CreateItemAsync(seller, price: 60));
        }

        var responses = await Task.WhenAll(items.Select(id => PurchaseAsync(buyer, id)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"Unexpected {r.StatusCode}"));
        Assert.Equal(40, await BalanceAsync(buyer));
        Assert.Equal(160, await BalanceAsync(seller));
    }

    [Fact]
    public async Task ConcurrentPurchases_OfTheSameItem_ChargeOnce()
    {
        var seller = await NewUserAsync();
        var buyer = await NewUserAsync();
        var itemId = await CreateItemAsync(seller, price: 10);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => PurchaseAsync(buyer, itemId)));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, $"Unexpected {r.StatusCode}"));
        Assert.Equal(90, await BalanceAsync(buyer));
        Assert.Equal(110, await BalanceAsync(seller));
    }

    // Read-modify-write would lose updates here: every request must add its 10.
    [Fact]
    public async Task ConcurrentRecharges_AreAllCounted()
    {
        var token = await NewUserAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            Api.SendAsync(HttpMethod.Patch, "/api/auth/wallet", token, new { amount = 10 })));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(200, await BalanceAsync(token));
    }

    // Two users buying from each other lock the same two rows: in opposite orders this would deadlock.
    [Fact]
    public async Task CrossPurchases_DoNotDeadlock()
    {
        var alice = await NewUserAsync();
        var bob = await NewUserAsync();
        var alicesItems = new List<string>();
        var bobsItems = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            alicesItems.Add(await CreateItemAsync(alice, price: 1));
            bobsItems.Add(await CreateItemAsync(bob, price: 1));
        }

        var responses = await Task.WhenAll(
            alicesItems.Select(id => PurchaseAsync(bob, id)).Concat(bobsItems.Select(id => PurchaseAsync(alice, id))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(100, await BalanceAsync(alice));
        Assert.Equal(100, await BalanceAsync(bob));
    }
}
