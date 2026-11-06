using System.Net;
using System.Text.Json;
using static ArtAround.ContractTests.TestData;

namespace ArtAround.ContractTests;

/*
 * GET    /api/visits/my                    - The caller's visits (auth)
 * GET    /api/visits/:idOrSlug             - Read one visit (answer keys for the creator only)
 * POST   /api/visits                       - Create visit (auth)
 * PUT    /api/visits/:id                   - Update visit (creator only)
 * DELETE /api/visits/:id                  - Delete visit (creator only)
 * GET    /api/museums/:idOrSlug/visits     - A museum's public visits, plus the caller's own
 * POST   /api/auth/favorites/visits/:id    - Toggle a saved visit (auth)
 * PATCH  /api/auth/language                - Set the caller's language (auth)
 */

/// <summary>An owner, another user, a museum and one public item, shared by every test in the class.</summary>
public class VisitFixture : IAsyncLifetime
{
    public const string VisitsApi = "/api/visits";

    public string OwnerToken { get; private set; } = "";
    public string OtherToken { get; private set; } = "";
    public string MuseumId { get; private set; } = "";
    public string ItemId { get; private set; } = "";

    public async Task InitializeAsync()
    {
        OwnerToken = await Api.RegisterAndGetTokenAsync(UniqueUsername(12));
        OtherToken = await Api.RegisterAndGetTokenAsync(UniqueUsername(12));

        using var museums = await Api.SendAsync(HttpMethod.Get, "/api/museums");
        MuseumId = (await Api.ReadJsonAsync(museums)).GetProperty("museums")[0].GetProperty("_id").GetString()!;

        using var contents = await Api.SendAsync(HttpMethod.Get, $"/api/museums/{MuseumId}/contents");
        var contentId = (await Api.ReadJsonAsync(contents)).GetProperty("contents")[0].GetProperty("universalId").GetString();

        using var item = await Api.SendAsync(HttpMethod.Post, "/api/items", OwnerToken, new
        {
            contentId,
            targetAudience = "general",
            isPublic = true,
            descriptions = new[] { new { tone = "easy", texts = new[] { new { text = "Stop", lengthCategory = "30s" } } } },
            texts = new[] { new { tone = "easy", text = "Stop" } },
        });
        Assert.Equal(HttpStatusCode.Created, item.StatusCode);
        ItemId = (await Api.ReadJsonAsync(item)).GetProperty("item").GetProperty("_id").GetString()!;
    }

    // The item stays: other test classes never see it, and deleting it would need every visit gone first.
    public Task DisposeAsync() => Task.CompletedTask;

    public object ValidBody(bool isPublic = true) => new
    {
        title = "Contract visit " + RandomString(8),
        museumId = MuseumId,
        isPublic,
        sequence = new[] { new { itemId = ItemId, nextDirections = "Turn left", prevDirections = "" } },
        blocks = new object[]
        {
            new { type = "artwork", blockName = "Main", items = new[] { ItemId } },
            new
            {
                type = "questions",
                blockName = "Check",
                questions = new[]
                {
                    new { prompt = "Which?", answerType = "multiple-choice", options = new[] { "A", "B" }, correctIndex = 1 },
                },
            },
        },
        quiz = new[] { new { question = "Who?", options = new[] { "A", "B" }, correctIndex = 0 } },
    };

    /// <summary>Creates a visit and returns the "visit" object of the response.</summary>
    public async Task<JsonElement> CreateVisitAsync(object? body = null)
    {
        using var response = await Api.SendAsync(HttpMethod.Post, VisitsApi, OwnerToken, body ?? ValidBody());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Api.ReadJsonAsync(response)).GetProperty("visit");
    }
}

public class VisitTests(VisitFixture fixture) : IClassFixture<VisitFixture>
{
    private static string VisitUrl(string id) => $"{VisitFixture.VisitsApi}/{id}";

    private static string Id(JsonElement visit) => visit.GetProperty("_id").GetString()!;

    private async Task<string> CreatedIdAsync(object? body = null) => Id(await fixture.CreateVisitAsync(body));

    /* ---------- create ---------- */

    [Fact]
    public async Task CreateVisit_Anonymous_Returns401()
    {
        using var response = await Api.SendAsync(HttpMethod.Post, VisitFixture.VisitsApi, body: fixture.ValidBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateVisit_MissingTitle_Returns400()
    {
        using var response = await Api.SendAsync(HttpMethod.Post, VisitFixture.VisitsApi, fixture.OwnerToken, new { museumId = fixture.MuseumId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("title", (await Api.ReadJsonAsync(response)).GetProperty("details")[0].GetProperty("field").GetString());
    }

    [Fact]
    public async Task CreateVisit_QuestionBlockWithoutQuestions_Returns400()
    {
        using var response = await Api.SendAsync(HttpMethod.Post, VisitFixture.VisitsApi, fixture.OwnerToken, new
        {
            title = "Invalid",
            museumId = fixture.MuseumId,
            blocks = new[] { new { type = "questions" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateVisit_ReturnsSlugAndDefaults()
    {
        var visit = await fixture.CreateVisitAsync(new { title = "Giro Città Nuova", museumId = fixture.MuseumId });

        Assert.Equal("giro-citta-nuova", visit.GetProperty("slug").GetString());
        Assert.Equal("standard", visit.GetProperty("type").GetString());
        Assert.Equal("normal", visit.GetProperty("length").GetString());
        Assert.True(visit.GetProperty("isPublic").GetBoolean());
    }

    [Fact]
    public async Task CreateVisit_PrivateStaysPrivate()
    {
        var visit = await fixture.CreateVisitAsync(fixture.ValidBody(isPublic: false));

        Assert.False(visit.GetProperty("isPublic").GetBoolean());
    }

    /* ---------- read ---------- */

    [Fact]
    public async Task GetVisit_ReturnsSequenceBlocksAndPopulatedRefs()
    {
        var id = await CreatedIdAsync();

        using var response = await Api.SendAsync(HttpMethod.Get, VisitUrl(id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var visit = (await Api.ReadJsonAsync(response)).GetProperty("visit");
        var stop = visit.GetProperty("sequence")[0];
        Assert.Equal(fixture.ItemId, stop.GetProperty("itemId").GetProperty("_id").GetString());
        Assert.Equal("Turn left", stop.GetProperty("nextDirections").GetString());
        Assert.Equal(0, stop.GetProperty("order").GetInt32());
        Assert.Equal(2, visit.GetProperty("blocks").GetArrayLength());
        Assert.Equal(fixture.MuseumId, visit.GetProperty("museumId").GetProperty("_id").GetString());
        Assert.True(visit.GetProperty("creatorId").TryGetProperty("username", out _));
    }

    [Fact]
    public async Task GetVisit_BySlug_ReturnsVisit()
    {
        var visit = await fixture.CreateVisitAsync();

        using var response = await Api.SendAsync(HttpMethod.Get, VisitUrl(visit.GetProperty("slug").GetString()!));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Id(visit), (await Api.ReadJsonAsync(response)).GetProperty("visit").GetProperty("_id").GetString());
    }

    [Fact]
    public async Task GetVisit_Unknown_Returns404()
    {
        using var response = await Api.SendAsync(HttpMethod.Get, VisitUrl("no-such-visit"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Visit not found", (await Api.ReadJsonAsync(response)).GetProperty("error").GetString());
    }

    // Students read the visit while answering: they must not get the answers with it.
    [Fact]
    public async Task GetVisit_AsOtherUser_HidesAnswerKeys()
    {
        var id = await CreatedIdAsync();

        using var response = await Api.SendAsync(HttpMethod.Get, VisitUrl(id), fixture.OtherToken);

        var visit = (await Api.ReadJsonAsync(response)).GetProperty("visit");
        Assert.False(visit.GetProperty("quiz")[0].TryGetProperty("correctIndex", out _));
        Assert.False(visit.GetProperty("blocks")[1].GetProperty("questions")[0].TryGetProperty("correctIndex", out _));
    }

    [Fact]
    public async Task GetVisit_AsCreator_ShowsAnswerKeys()
    {
        var id = await CreatedIdAsync();

        using var response = await Api.SendAsync(HttpMethod.Get, VisitUrl(id), fixture.OwnerToken);

        var visit = (await Api.ReadJsonAsync(response)).GetProperty("visit");
        Assert.Equal(0, visit.GetProperty("quiz")[0].GetProperty("correctIndex").GetInt32());
        Assert.Equal(1, visit.GetProperty("blocks")[1].GetProperty("questions")[0].GetProperty("correctIndex").GetInt32());
    }

    [Fact]
    public async Task MyVisits_ContainsOwnVisitsOnly()
    {
        var id = await CreatedIdAsync();

        using var mine = await Api.SendAsync(HttpMethod.Get, $"{VisitFixture.VisitsApi}/my", fixture.OwnerToken);
        using var others = await Api.SendAsync(HttpMethod.Get, $"{VisitFixture.VisitsApi}/my", fixture.OtherToken);

        Assert.Contains((await Api.ReadJsonAsync(mine)).GetProperty("visits").EnumerateArray(), v => Id(v) == id);
        Assert.DoesNotContain((await Api.ReadJsonAsync(others)).GetProperty("visits").EnumerateArray(), v => Id(v) == id);
    }

    [Fact]
    public async Task MyVisits_Anonymous_Returns401()
    {
        using var response = await Api.SendAsync(HttpMethod.Get, $"{VisitFixture.VisitsApi}/my");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MuseumVisits_HidePrivateVisitFromOthers()
    {
        var id = await CreatedIdAsync(fixture.ValidBody(isPublic: false));
        var url = $"/api/museums/{fixture.MuseumId}/visits";

        using var owner = await Api.SendAsync(HttpMethod.Get, url, fixture.OwnerToken);
        using var anonymous = await Api.SendAsync(HttpMethod.Get, url);

        Assert.Contains((await Api.ReadJsonAsync(owner)).GetProperty("visits").EnumerateArray(), v => Id(v) == id);
        Assert.DoesNotContain((await Api.ReadJsonAsync(anonymous)).GetProperty("visits").EnumerateArray(), v => Id(v) == id);
    }

    /* ---------- update ---------- */

    [Fact]
    public async Task UpdateVisit_AsOwner_ChangesOnlySentFields()
    {
        var id = await CreatedIdAsync();

        using var response = await Api.SendAsync(HttpMethod.Put, VisitUrl(id), fixture.OwnerToken, new { description = "New" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var visit = (await Api.ReadJsonAsync(response)).GetProperty("visit");
        Assert.Equal("New", visit.GetProperty("description").GetString());
        Assert.Equal(1, visit.GetProperty("sequence").GetArrayLength());
    }

    // Replacing the children twice proves the old rows really go away.
    [Fact]
    public async Task UpdateVisit_ReplacesChildren()
    {
        var id = await CreatedIdAsync();
        var replacement = new
        {
            sequence = new[] { new { itemId = fixture.ItemId, nextDirections = "Second", prevDirections = "" } },
            blocks = new[] { new { type = "artwork", blockName = "Only", items = new[] { fixture.ItemId } } },
            quiz = new[] { new { question = "Again?", options = new[] { "Y", "N" }, correctIndex = 1 } },
        };

        using var first = await Api.SendAsync(HttpMethod.Put, VisitUrl(id), fixture.OwnerToken, replacement);
        using var second = await Api.SendAsync(HttpMethod.Put, VisitUrl(id), fixture.OwnerToken, replacement);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var read = await Api.SendAsync(HttpMethod.Get, VisitUrl(id), fixture.OwnerToken);
        var visit = (await Api.ReadJsonAsync(read)).GetProperty("visit");
        Assert.Equal("Only", visit.GetProperty("blocks")[0].GetProperty("blockName").GetString());
        Assert.Equal(1, visit.GetProperty("blocks").GetArrayLength());
        Assert.Equal(1, visit.GetProperty("quiz").GetArrayLength());
        Assert.Equal("Second", visit.GetProperty("sequence")[0].GetProperty("nextDirections").GetString());
    }

    [Fact]
    public async Task UpdateVisit_AsOtherUser_Returns403()
    {
        var id = await CreatedIdAsync();

        using var response = await Api.SendAsync(HttpMethod.Put, VisitUrl(id), fixture.OtherToken, new { description = "Hack" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /* ---------- delete ---------- */

    [Fact]
    public async Task DeleteVisit_AsOtherUser_Returns403()
    {
        var id = await CreatedIdAsync();

        using var response = await Api.SendAsync(HttpMethod.Delete, VisitUrl(id), fixture.OtherToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVisit_AsOwner_ThenReturns404()
    {
        var id = await CreatedIdAsync();

        using var deleted = await Api.SendAsync(HttpMethod.Delete, VisitUrl(id), fixture.OwnerToken);
        using var read = await Api.SendAsync(HttpMethod.Get, VisitUrl(id));

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task DeleteItem_UsedByVisit_Returns409()
    {
        await CreatedIdAsync();

        using var response = await Api.SendAsync(HttpMethod.Delete, $"/api/items/{fixture.ItemId}", fixture.OwnerToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /* ---------- favorites and language ---------- */

    [Fact]
    public async Task ToggleFavorite_SavesThenRemoves()
    {
        var id = await CreatedIdAsync();
        var url = $"/api/auth/favorites/visits/{id}";

        using var saved = await Api.SendAsync(HttpMethod.Post, url, fixture.OtherToken);
        using var me = await Api.SendAsync(HttpMethod.Get, "/api/auth/me", fixture.OtherToken);
        using var removed = await Api.SendAsync(HttpMethod.Post, url, fixture.OtherToken);

        Assert.True((await Api.ReadJsonAsync(saved)).GetProperty("isSaved").GetBoolean());
        Assert.Contains((await Api.ReadJsonAsync(me)).GetProperty("user").GetProperty("savedVisits").EnumerateArray(), v => Id(v) == id);
        Assert.False((await Api.ReadJsonAsync(removed)).GetProperty("isSaved").GetBoolean());
    }

    [Fact]
    public async Task UpdateLanguage_ChangesLanguage()
    {
        var token = await Api.RegisterAndGetTokenAsync(UniqueUsername(12));

        using var response = await Api.SendAsync(HttpMethod.Patch, "/api/auth/language", token, new { language = "en" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("en", (await Api.ReadJsonAsync(response)).GetProperty("language").GetString());
    }

    [Fact]
    public async Task UpdateLanguage_Unsupported_Returns400()
    {
        using var response = await Api.SendAsync(HttpMethod.Patch, "/api/auth/language", fixture.OtherToken, new { language = "fr" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
