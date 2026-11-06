using System.Net;
using System.Text.Json;
using static ArtAround.ContractTests.TestData;

namespace ArtAround.ContractTests;

/*
 * Group visits over REST. Every mutation goes through these endpoints; the real-time
 * connection only broadcasts the results.
 *
 * POST /api/sessions                                - Create (auth, synchronized visits only)
 * GET  /api/sessions/my                             - The caller's hosted sessions
 * GET  /api/sessions/:code                          - Read an active session
 * POST /api/sessions/:code/join | leave             - Membership
 * POST /api/sessions/:code/advance | previous | end - Host controls
 * POST /api/sessions/:code/activity | message       - Activity log and chat
 * POST /api/sessions/:code/sections/:id/answers     - Answer a question section
 * GET  /api/sessions/:code/sections/:id/responses   - All answers (host only)
 * POST /api/sessions/:code/quiz/start               - Start the quiz (host only)
 * POST /api/sessions/:code/quiz                     - Submit the quiz
 * GET  /api/sessions/:code/quiz                     - Quiz results (host only)
 */

/// <summary>A host, two students and a synchronized visit: artwork, question section, artwork.</summary>
public class SessionFixture : IAsyncLifetime
{
    public const string SessionsApi = "/api/sessions";

    public string HostToken { get; private set; } = "";
    public string StudentToken { get; private set; } = "";
    public string OutsiderToken { get; private set; } = "";
    public string VisitId { get; private set; } = "";
    public string StandardVisitId { get; private set; } = "";
    public string SectionId { get; private set; } = "";
    public string MultipleChoiceId { get; private set; } = "";
    public string OpenQuestionId { get; private set; } = "";

    public async Task InitializeAsync()
    {
        HostToken = await Api.RegisterAndGetTokenAsync(UniqueUsername(12));
        StudentToken = await Api.RegisterAndGetTokenAsync(UniqueUsername(12));
        OutsiderToken = await Api.RegisterAndGetTokenAsync(UniqueUsername(12));

        using var museums = await Api.SendAsync(HttpMethod.Get, "/api/museums");
        var museumId = (await Api.ReadJsonAsync(museums)).GetProperty("museums")[0].GetProperty("_id").GetString();
        using var contents = await Api.SendAsync(HttpMethod.Get, $"/api/museums/{museumId}/contents");
        var contentId = (await Api.ReadJsonAsync(contents)).GetProperty("contents")[0].GetProperty("universalId").GetString();

        using var item = await Api.SendAsync(HttpMethod.Post, "/api/items", HostToken, new
        {
            contentId,
            targetAudience = "general",
            isPublic = true,
            descriptions = new[] { new { tone = "easy", texts = new[] { new { text = "Stop", lengthCategory = "30s" } } } },
            texts = new[] { new { tone = "easy", text = "Stop" } },
        });
        var itemId = (await Api.ReadJsonAsync(item)).GetProperty("item").GetProperty("_id").GetString()!;

        using var visit = await Api.SendAsync(HttpMethod.Post, "/api/visits", HostToken, new
        {
            title = "Group visit",
            museumId,
            type = "synchronized",
            sequence = new[] { new { itemId }, new { itemId } },
            blocks = new object[]
            {
                new { type = "artwork", blockName = "First", items = new[] { itemId } },
                new
                {
                    type = "questions",
                    blockName = "Check",
                    questions = new object[]
                    {
                        new { prompt = "Which?", answerType = "multiple-choice", options = new[] { "A", "B" }, correctIndex = 1 },
                        new { prompt = "Why?", answerType = "open" },
                    },
                },
                new { type = "artwork", blockName = "Last", items = new[] { itemId } },
            },
            quiz = new[] { new { question = "Who?", options = new[] { "A", "B" }, correctIndex = 1 } },
        });
        Assert.Equal(HttpStatusCode.Created, visit.StatusCode);
        var created = (await Api.ReadJsonAsync(visit)).GetProperty("visit");
        VisitId = created.GetProperty("_id").GetString()!;
        var section = created.GetProperty("blocks")[1];
        SectionId = section.GetProperty("_id").GetString()!;
        MultipleChoiceId = section.GetProperty("questions")[0].GetProperty("_id").GetString()!;
        OpenQuestionId = section.GetProperty("questions")[1].GetProperty("_id").GetString()!;

        using var standard = await Api.SendAsync(HttpMethod.Post, "/api/visits", HostToken, new { title = "Solo", museumId });
        StandardVisitId = (await Api.ReadJsonAsync(standard)).GetProperty("visit").GetProperty("_id").GetString()!;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Starts a session of its own, so tests never share position, chat or answers.</summary>
    public async Task<string> CreateSessionAsync()
    {
        using var response = await Api.SendAsync(HttpMethod.Post, SessionsApi, HostToken, new { visitId = VisitId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Api.ReadJsonAsync(response)).GetProperty("session").GetProperty("code").GetString()!;
    }
}

public class SessionTests(SessionFixture fixture) : IClassFixture<SessionFixture>
{
    private static string Url(string code, string path = "") => $"{SessionFixture.SessionsApi}/{Uri.EscapeDataString(code)}{path}";

    private static Task<HttpResponseMessage> PostAsync(string token, string code, string path, object? body = null) =>
        Api.SendAsync(HttpMethod.Post, Url(code, path), token, body);

    private async Task<string> SessionWithStudentAsync()
    {
        var code = await fixture.CreateSessionAsync();
        using var join = await PostAsync(fixture.StudentToken, code, "/join");
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        return code;
    }

    private static async Task<JsonElement> ReadSessionAsync(string token, string code)
    {
        using var response = await Api.SendAsync(HttpMethod.Get, Url(code), token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Api.ReadJsonAsync(response)).GetProperty("session");
    }

    private static bool IsHidden(JsonElement parent, string name) =>
        !parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null;

    /* ---------- create ---------- */

    [Fact]
    public async Task Create_Anonymous_Returns401()
    {
        using var response = await Api.SendAsync(HttpMethod.Post, SessionFixture.SessionsApi, body: new { visitId = fixture.VisitId });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_StandardVisit_Returns400()
    {
        using var response = await Api.SendAsync(HttpMethod.Post, SessionFixture.SessionsApi, fixture.HostToken, new { visitId = fixture.StandardVisitId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_CustomCode_IsUppercasedAndUniqueAmongActive()
    {
        var code = "room-" + RandomString(8);

        using var first = await Api.SendAsync(HttpMethod.Post, SessionFixture.SessionsApi, fixture.HostToken, new { visitId = fixture.VisitId, code });
        using var second = await Api.SendAsync(HttpMethod.Post, SessionFixture.SessionsApi, fixture.HostToken, new { visitId = fixture.VisitId, code });

        Assert.Equal(code.ToUpperInvariant(), (await Api.ReadJsonAsync(first)).GetProperty("session").GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /* ---------- reading and joining ---------- */

    [Fact]
    public async Task Read_UnknownCode_Returns404()
    {
        using var response = await Api.SendAsync(HttpMethod.Get, Url("NO_SUCH_ROOM"), fixture.HostToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Read_TellsTheCallerItsRole()
    {
        var code = await SessionWithStudentAsync();

        var asHost = await ReadSessionAsync(fixture.HostToken, code);
        var asStudent = await ReadSessionAsync(fixture.StudentToken, code);

        Assert.True(asHost.GetProperty("isOwner").GetBoolean());
        Assert.False(asStudent.GetProperty("isOwner").GetBoolean());
        Assert.Equal(1, asHost.GetProperty("participants").GetArrayLength());
        Assert.Equal(1, asHost.GetProperty("participantCount").GetInt32());
    }

    // A student opens the session while it runs: the answers must not come with it.
    [Fact]
    public async Task Read_HidesAnswerKeysFromStudents()
    {
        var code = await SessionWithStudentAsync();

        var asHost = (await ReadSessionAsync(fixture.HostToken, code)).GetProperty("visitId");
        var asStudent = (await ReadSessionAsync(fixture.StudentToken, code)).GetProperty("visitId");

        Assert.Equal(1, asHost.GetProperty("quiz")[0].GetProperty("correctIndex").GetInt32());
        Assert.True(IsHidden(asStudent.GetProperty("quiz")[0], "correctIndex"));
        Assert.True(IsHidden(asStudent.GetProperty("blocks")[1].GetProperty("questions")[0], "correctIndex"));
    }

    [Fact]
    public async Task Leave_MarksParticipantInactive()
    {
        var code = await SessionWithStudentAsync();

        using var leave = await PostAsync(fixture.StudentToken, code, "/leave");

        Assert.Equal(HttpStatusCode.OK, leave.StatusCode);
        Assert.Equal(0, (await ReadSessionAsync(fixture.HostToken, code)).GetProperty("participantCount").GetInt32());
    }

    [Fact]
    public async Task MySessions_ListsHostedSession()
    {
        var code = await fixture.CreateSessionAsync();

        using var response = await Api.SendAsync(HttpMethod.Get, $"{SessionFixture.SessionsApi}/my", fixture.HostToken);

        Assert.Contains((await Api.ReadJsonAsync(response)).GetProperty("sessions").EnumerateArray(), s => s.GetProperty("code").GetString() == code);
    }

    /* ---------- host controls ---------- */

    [Fact]
    public async Task Advance_WalksArtworkThenQuestionsThenArtwork()
    {
        var code = await fixture.CreateSessionAsync();

        using var toQuestions = await PostAsync(fixture.HostToken, code, "/advance");
        using var toLast = await PostAsync(fixture.HostToken, code, "/advance");
        using var pastTheEnd = await PostAsync(fixture.HostToken, code, "/advance");

        var questions = await Api.ReadJsonAsync(toQuestions);
        Assert.Equal(1, questions.GetProperty("currentStepIndex").GetInt32());
        Assert.Equal("questions", questions.GetProperty("step").GetProperty("type").GetString());
        var last = await Api.ReadJsonAsync(toLast);
        Assert.Equal(1, last.GetProperty("currentItemIndex").GetInt32());
        Assert.True(last.GetProperty("isLast").GetBoolean());
        Assert.Equal(2, (await Api.ReadJsonAsync(pastTheEnd)).GetProperty("currentStepIndex").GetInt32());
    }

    [Fact]
    public async Task Previous_StopsAtTheStart()
    {
        var code = await fixture.CreateSessionAsync();
        using var advance = await PostAsync(fixture.HostToken, code, "/advance");

        using var back = await PostAsync(fixture.HostToken, code, "/previous");
        using var pastTheStart = await PostAsync(fixture.HostToken, code, "/previous");

        Assert.Equal(0, (await Api.ReadJsonAsync(back)).GetProperty("currentStepIndex").GetInt32());
        var first = await Api.ReadJsonAsync(pastTheStart);
        Assert.Equal(0, first.GetProperty("currentStepIndex").GetInt32());
        Assert.True(first.GetProperty("isFirst").GetBoolean());
    }

    [Theory]
    [InlineData("/advance")]
    [InlineData("/previous")]
    [InlineData("/end")]
    [InlineData("/quiz/start")]
    public async Task HostControls_AsStudent_Return403(string path)
    {
        var code = await SessionWithStudentAsync();

        using var response = await PostAsync(fixture.StudentToken, code, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task End_MakesTheSessionUnavailable()
    {
        var code = await fixture.CreateSessionAsync();

        using var end = await PostAsync(fixture.HostToken, code, "/end");
        using var read = await Api.SendAsync(HttpMethod.Get, Url(code), fixture.HostToken);

        Assert.Equal(HttpStatusCode.OK, end.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    /* ---------- activity and chat ---------- */

    [Fact]
    public async Task Activity_IsLogged()
    {
        var code = await SessionWithStudentAsync();

        using var response = await PostAsync(fixture.StudentToken, code, "/activity", new { action = "map" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // joined and left are written by the server: a client can't forge them.
    [Fact]
    public async Task Activity_ForgedAction_Returns400()
    {
        var code = await SessionWithStudentAsync();

        using var response = await PostAsync(fixture.StudentToken, code, "/activity", new { action = "joined" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Message_IsStoredForLaterReaders()
    {
        var code = await SessionWithStudentAsync();

        using var response = await PostAsync(fixture.StudentToken, code, "/message", new { text = "  Ciao  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Ciao", (await Api.ReadJsonAsync(response)).GetProperty("message").GetProperty("text").GetString());
    }

    [Fact]
    public async Task Message_FromOutsider_Returns403()
    {
        var code = await fixture.CreateSessionAsync();

        using var response = await PostAsync(fixture.OutsiderToken, code, "/message", new { text = "Hi" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Message_Empty_Returns400()
    {
        var code = await SessionWithStudentAsync();

        using var response = await PostAsync(fixture.StudentToken, code, "/message", new { text = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /* ---------- question sections ---------- */

    private async Task<string> SessionOnQuestionsAsync()
    {
        var code = await SessionWithStudentAsync();
        using var advance = await PostAsync(fixture.HostToken, code, "/advance");
        return code;
    }

    private string AnswersPath => $"/sections/{fixture.SectionId}/answers";

    [Fact]
    public async Task Answer_SectionNotActive_Returns409()
    {
        var code = await SessionWithStudentAsync();

        using var response = await PostAsync(fixture.StudentToken, code, AnswersPath, new { questionId = fixture.OpenQuestionId, text = "Early" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Answer_HidesCorrectnessFromStudentButShowsHost()
    {
        var code = await SessionOnQuestionsAsync();

        using var answer = await PostAsync(fixture.StudentToken, code, AnswersPath, new { questionId = fixture.MultipleChoiceId, selectedIndex = 1 });
        using var responses = await Api.SendAsync(HttpMethod.Get, Url(code, $"/sections/{fixture.SectionId}/responses"), fixture.HostToken);

        Assert.Equal(HttpStatusCode.Created, answer.StatusCode);
        Assert.True(IsHidden((await Api.ReadJsonAsync(answer)).GetProperty("response"), "isCorrect"));
        Assert.True((await Api.ReadJsonAsync(responses)).GetProperty("responses")[0].GetProperty("isCorrect").GetBoolean());
    }

    [Fact]
    public async Task Answer_SecondTime_ReplacesTheFirst()
    {
        var code = await SessionOnQuestionsAsync();

        using var first = await PostAsync(fixture.StudentToken, code, AnswersPath, new { questionId = fixture.OpenQuestionId, text = "First" });
        using var second = await PostAsync(fixture.StudentToken, code, AnswersPath, new { questionId = fixture.OpenQuestionId, text = "Second" });
        using var responses = await Api.SendAsync(HttpMethod.Get, Url(code, $"/sections/{fixture.SectionId}/responses"), fixture.HostToken);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var list = (await Api.ReadJsonAsync(responses)).GetProperty("responses");
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("Second", list[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Answer_InvalidOption_Returns400()
    {
        var code = await SessionOnQuestionsAsync();

        using var response = await PostAsync(fixture.StudentToken, code, AnswersPath, new { questionId = fixture.MultipleChoiceId, selectedIndex = 9 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Answer_FromOutsider_Returns403()
    {
        var code = await SessionOnQuestionsAsync();

        using var response = await PostAsync(fixture.OutsiderToken, code, AnswersPath, new { questionId = fixture.OpenQuestionId, text = "Hi" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Responses_AsStudent_Return403()
    {
        var code = await SessionOnQuestionsAsync();

        using var response = await Api.SendAsync(HttpMethod.Get, Url(code, $"/sections/{fixture.SectionId}/responses"), fixture.StudentToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /* ---------- quiz ---------- */

    private static object QuizAnswers(int selectedIndex) => new { answers = new[] { new { questionIndex = 0, selectedIndex } } };

    [Fact]
    public async Task Quiz_IsScoredOnTheServerAndOnlyOnce()
    {
        var code = await SessionWithStudentAsync();

        using var submit = await PostAsync(fixture.StudentToken, code, "/quiz", QuizAnswers(1));
        using var again = await PostAsync(fixture.StudentToken, code, "/quiz", QuizAnswers(0));

        var result = await Api.ReadJsonAsync(submit);
        Assert.Equal(1, result.GetProperty("score").GetInt32());
        Assert.Equal(1, result.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Quiz_FromOutsider_Returns403()
    {
        var code = await fixture.CreateSessionAsync();

        using var response = await PostAsync(fixture.OutsiderToken, code, "/quiz", QuizAnswers(1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task QuizResults_ForHostOnly()
    {
        var code = await SessionWithStudentAsync();
        using var submit = await PostAsync(fixture.StudentToken, code, "/quiz", QuizAnswers(1));

        using var asHost = await Api.SendAsync(HttpMethod.Get, Url(code, "/quiz"), fixture.HostToken);
        using var asStudent = await Api.SendAsync(HttpMethod.Get, Url(code, "/quiz"), fixture.StudentToken);

        var result = (await Api.ReadJsonAsync(asHost)).GetProperty("results")[0];
        Assert.Equal(1, result.GetProperty("quizScore").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, asStudent.StatusCode);
    }

    [Fact]
    public async Task Read_HidesOtherParticipantsScores()
    {
        var code = await SessionWithStudentAsync();
        using var otherJoin = await PostAsync(fixture.OutsiderToken, code, "/join");
        using var submit = await PostAsync(fixture.StudentToken, code, "/quiz", QuizAnswers(1));

        var asOther = await ReadSessionAsync(fixture.OutsiderToken, code);

        Assert.All(asOther.GetProperty("participants").EnumerateArray(), p => Assert.True(IsHidden(p, "quizScore")));
    }

    [Fact]
    public async Task QuizStart_WithQuiz_IsPersisted()
    {
        var code = await SessionWithStudentAsync();

        using var start = await PostAsync(fixture.HostToken, code, "/quiz/start");

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.True((await ReadSessionAsync(fixture.StudentToken, code)).GetProperty("quizStarted").GetBoolean());
    }
}
