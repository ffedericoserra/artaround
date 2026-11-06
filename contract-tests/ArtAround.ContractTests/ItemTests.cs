using System.Net;
using static ArtAround.ContractTests.TestData;

namespace ArtAround.ContractTests;

/*
 * GET    /api/items/:id - Read one item (public, or private to creator/buyer)
 * POST   /api/items     - Create item (auth)
 * GET    /api/items     - List items (public, plus the caller's own)
 * PUT    /api/items/:id - Update item (creator only)
 * DELETE /api/items/:id - Delete item (creator only)
 */

/// <summary>An owner, another user and one private item, shared by every test in the class.</summary>
public class PrivateItemFixture : IAsyncLifetime
{
    public const string ItemsApi = "/api/items";

    public string OwnerToken { get; private set; } = "";
    public string OtherToken { get; private set; } = "";
    public string PrivateItemId { get; private set; } = "";
    private string _contentId = "";

    public async Task InitializeAsync()
    {
        OwnerToken = await Api.RegisterAndGetTokenAsync(UniqueUsername(12));
        OtherToken = await Api.RegisterAndGetTokenAsync(UniqueUsername(12));
        _contentId = await AnyContentIdAsync();
        PrivateItemId = await CreateItemAsync(OwnerToken, isPublic: false);
    }

    // Unlike users, items can be deleted through the API, so clean up.
    public async Task DisposeAsync() => await DeleteItemAsync(OwnerToken, PrivateItemId);

    // Carries both text shapes until the cutover: Node reads descriptions, .NET reads texts,
    // and each ignores the other.
    public object ValidBody(bool isPublic) => new
    {
        contentId = _contentId,
        targetAudience = "general",
        isPublic,
        descriptions = new[]
        {
            new
            {
                tone = "easy",
                texts = new[] { new { text = "Contract test description", lengthCategory = "30s" } },
            },
        },
        texts = new[] { new { tone = "easy", text = "Contract test description" } },
    };

    public async Task<string> CreateItemAsync(string token, bool isPublic)
    {
        using var response = await Api.SendAsync(HttpMethod.Post, ItemsApi, token, ValidBody(isPublic));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await Api.ReadJsonAsync(response);
        return body.GetProperty("item").GetProperty("_id").GetString()!;
    }

    public static async Task DeleteItemAsync(string token, string id)
    {
        using var response = await Api.SendAsync(HttpMethod.Delete, $"{ItemsApi}/{id}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>universalId of the first content of the first seeded museum.</summary>
    private static async Task<string> AnyContentIdAsync()
    {
        using var museums = await Api.SendAsync(HttpMethod.Get, "/api/museums");
        Assert.Equal(HttpStatusCode.OK, museums.StatusCode);
        var museumId = (await Api.ReadJsonAsync(museums))
            .GetProperty("museums")[0].GetProperty("_id").GetString();

        using var contents = await Api.SendAsync(HttpMethod.Get, $"/api/museums/{museumId}/contents");
        Assert.Equal(HttpStatusCode.OK, contents.StatusCode);
        return (await Api.ReadJsonAsync(contents))
            .GetProperty("contents")[0].GetProperty("universalId").GetString()!;
    }
}

public class ItemTests(PrivateItemFixture fixture) : IClassFixture<PrivateItemFixture>
{
    private static string ItemUrl(string id) => $"{PrivateItemFixture.ItemsApi}/{id}";

    /* ---------- ownership ---------- */

    [Fact]
    public async Task GetPrivateItem_AsOwner_Returns200()
    {
        using var response = await Api.SendAsync(HttpMethod.Get, ItemUrl(fixture.PrivateItemId), fixture.OwnerToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = (await Api.ReadJsonAsync(response)).GetProperty("item");
        Assert.Equal(fixture.PrivateItemId, item.GetProperty("_id").GetString());
        Assert.True(item.GetProperty("isOwned").GetBoolean());
    }

    [Fact]
    public async Task GetPrivateItem_AsOtherUser_Returns403()
    {
        using var response = await Api.SendAsync(HttpMethod.Get, ItemUrl(fixture.PrivateItemId), fixture.OtherToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False((await Api.ReadJsonAsync(response)).TryGetProperty("item", out _));
    }

    [Fact]
    public async Task GetPrivateItem_Anonymous_Returns403()
    {
        using var response = await Api.SendAsync(HttpMethod.Get, ItemUrl(fixture.PrivateItemId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False((await Api.ReadJsonAsync(response)).TryGetProperty("item", out _));
    }

    /* ---------- not found ---------- */

    // A deleted item's id is well-formed in whatever id format the backend uses
    // (Mongo ObjectId today, bigint in .NET). A hard-coded id would not be.
    [Fact]
    public async Task GetItem_Deleted_Returns404()
    {
        var id = await fixture.CreateItemAsync(fixture.OwnerToken, isPublic: true);
        await PrivateItemFixture.DeleteItemAsync(fixture.OwnerToken, id);

        using var response = await Api.SendAsync(HttpMethod.Get, ItemUrl(id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Item not found", (await Api.ReadJsonAsync(response)).GetProperty("error").GetString());
    }

    // Malformed in every id format. A client error, so 400, never 500.
    [Fact]
    public async Task GetItem_MalformedId_Returns400()
    {
        using var response = await Api.SendAsync(HttpMethod.Get, ItemUrl("not-an-id"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("ObjectId", (await Api.ReadJsonAsync(response)).GetProperty("error").GetString());
    }

    /* ---------- create ---------- */

    [Fact]
    public async Task CreateItem_Anonymous_Returns401()
    {
        using var response = await Api.SendAsync(HttpMethod.Post, PrivateItemFixture.ItemsApi, body: fixture.ValidBody(isPublic: true));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateItem_MissingTargetAudience_Returns400()
    {
        using var response = await Api.SendAsync(HttpMethod.Post, PrivateItemFixture.ItemsApi, fixture.OwnerToken, new
        {
            contentId = "any",
            descriptions = Array.Empty<object>(),
            texts = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateItem_WithoutPrice_IsFree()
    {
        var id = await fixture.CreateItemAsync(fixture.OwnerToken, isPublic: false);
        try
        {
            using var response = await Api.SendAsync(HttpMethod.Get, ItemUrl(id), fixture.OwnerToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var item = (await Api.ReadJsonAsync(response)).GetProperty("item");
            Assert.False(item.GetProperty("isPublic").GetBoolean());
            Assert.Equal(0, item.GetProperty("price").GetInt32());
        }
        finally
        {
            await PrivateItemFixture.DeleteItemAsync(fixture.OwnerToken, id);
        }
    }

    /* ---------- list ---------- */

    private static async Task<List<string?>> ListItemIdsAsync(string? token)
    {
        using var response = await Api.SendAsync(HttpMethod.Get, PrivateItemFixture.ItemsApi, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Api.ReadJsonAsync(response)).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("_id").GetString())
            .ToList();
    }

    [Fact]
    public async Task ListItems_OwnerSeesOwnPrivateItem()
    {
        Assert.Contains(fixture.PrivateItemId, await ListItemIdsAsync(fixture.OwnerToken));
    }

    [Fact]
    public async Task ListItems_AnonymousAndOthersDoNotSeePrivateItem()
    {
        Assert.DoesNotContain(fixture.PrivateItemId, await ListItemIdsAsync(null));
        Assert.DoesNotContain(fixture.PrivateItemId, await ListItemIdsAsync(fixture.OtherToken));
    }

    /* ---------- update ---------- */

    [Fact]
    public async Task UpdateItem_AsOwner_ChangesOnlySentFields()
    {
        var id = await fixture.CreateItemAsync(fixture.OwnerToken, isPublic: false);
        try
        {
            using var response = await Api.SendAsync(HttpMethod.Put, ItemUrl(id), fixture.OwnerToken, new { price = 5 });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var item = (await Api.ReadJsonAsync(response)).GetProperty("item");
            Assert.Equal(5, item.GetProperty("price").GetInt32());
            Assert.Equal("general", item.GetProperty("targetAudience").GetString());
        }
        finally
        {
            await PrivateItemFixture.DeleteItemAsync(fixture.OwnerToken, id);
        }
    }

    [Fact]
    public async Task UpdateItem_AsOtherUser_Returns403()
    {
        using var response = await Api.SendAsync(HttpMethod.Put, ItemUrl(fixture.PrivateItemId), fixture.OtherToken, new { price = 5 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_Anonymous_Returns401()
    {
        using var response = await Api.SendAsync(HttpMethod.Put, ItemUrl(fixture.PrivateItemId), body: new { price = 5 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_NegativePrice_Returns400()
    {
        using var response = await Api.SendAsync(HttpMethod.Put, ItemUrl(fixture.PrivateItemId), fixture.OwnerToken, new { price = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /* ---------- delete ---------- */

    [Fact]
    public async Task DeleteItem_AsOtherUser_Returns403()
    {
        using var response = await Api.SendAsync(HttpMethod.Delete, ItemUrl(fixture.PrivateItemId), fixture.OtherToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
