using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace ArtAround.ContractTests;

/*
 * The SignalR hub at /hubs/session. Clients only join and listen: every change is
 * made over REST, and these tests check the broadcast that follows.
 */

public class RealtimeTests(SessionFixture fixture) : IClassFixture<SessionFixture>, IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly List<HubConnection> _connections = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
    }

    private HubConnection NewConnection(string? token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Api.Client.BaseAddress!, "/hubs/session"), options => options.AccessTokenProvider = () => Task.FromResult(token))
            .Build();
        _connections.Add(connection);
        return connection;
    }

    /// <summary>Completes with the first payload of an event. Subscribe before the action that triggers it.</summary>
    private static Task<JsonElement> NextEvent(HubConnection connection, string name)
    {
        var received = new TaskCompletionSource<JsonElement>();
        connection.On<JsonElement>(name, payload => received.TrySetResult(payload));
        return received.Task.WaitAsync(Timeout);
    }

    private async Task<string> SessionWithStudentAsync()
    {
        var code = await fixture.CreateSessionAsync();
        using var join = await Api.SendAsync(HttpMethod.Post, $"{SessionFixture.SessionsApi}/{code}/join", fixture.StudentToken);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        return code;
    }

    private async Task<HubConnection> JoinedConnectionAsync(string token, string code)
    {
        var connection = NewConnection(token);
        var state = NextEvent(connection, "session:state");
        await connection.StartAsync();
        await connection.InvokeAsync("JoinSession", code);
        await state;
        return connection;
    }

    [Fact]
    public async Task Connect_WithoutToken_IsRejected()
    {
        var connection = NewConnection(null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task Join_SendsCatchUpState()
    {
        var code = await SessionWithStudentAsync();
        using var message = await Api.SendAsync(HttpMethod.Post, $"{SessionFixture.SessionsApi}/{code}/message", fixture.StudentToken, new { text = "Earlier" });
        var connection = NewConnection(fixture.StudentToken);
        var state = NextEvent(connection, "session:state");

        await connection.StartAsync();
        await connection.InvokeAsync("JoinSession", code);

        var received = await state;
        Assert.Equal("Earlier", received.GetProperty("messages")[0].GetProperty("text").GetString());
        Assert.Equal(1, received.GetProperty("participantCount").GetInt32());
    }

    [Fact]
    public async Task Join_UnknownSession_SendsError()
    {
        var connection = NewConnection(fixture.StudentToken);
        var error = NextEvent(connection, "session:error");

        await connection.StartAsync();
        await connection.InvokeAsync("JoinSession", "NO_SUCH_ROOM");

        Assert.Equal("Session not found", (await error).GetString());
    }

    [Fact]
    public async Task Message_ReachesTheRoom()
    {
        var code = await SessionWithStudentAsync();
        var host = await JoinedConnectionAsync(fixture.HostToken, code);
        var chat = NextEvent(host, "session:chat");

        using var message = await Api.SendAsync(HttpMethod.Post, $"{SessionFixture.SessionsApi}/{code}/message", fixture.StudentToken, new { text = "Ciao" });

        Assert.Equal("Ciao", (await chat).GetProperty("text").GetString());
    }

    [Fact]
    public async Task Advance_MovesEveryone()
    {
        var code = await SessionWithStudentAsync();
        var student = await JoinedConnectionAsync(fixture.StudentToken, code);
        var step = NextEvent(student, "session:step-changed");

        using var advance = await Api.SendAsync(HttpMethod.Post, $"{SessionFixture.SessionsApi}/{code}/advance", fixture.HostToken);

        Assert.Equal(1, (await step).GetProperty("currentStepIndex").GetInt32());
    }

    // The student's own connection must never carry whether an answer is right.
    [Fact]
    public async Task SectionResponse_ReachesOnlyTheHost()
    {
        var code = await SessionWithStudentAsync();
        var host = await JoinedConnectionAsync(fixture.HostToken, code);
        var student = await JoinedConnectionAsync(fixture.StudentToken, code);
        var atHost = NextEvent(host, "session:section-response");
        var atStudent = NextEvent(student, "session:section-response");
        using var advance = await Api.SendAsync(HttpMethod.Post, $"{SessionFixture.SessionsApi}/{code}/advance", fixture.HostToken);

        using var answer = await Api.SendAsync(HttpMethod.Post, $"{SessionFixture.SessionsApi}/{code}/sections/{fixture.SectionId}/answers", fixture.StudentToken,
            new { questionId = fixture.MultipleChoiceId, selectedIndex = 1 });

        Assert.True((await atHost).GetProperty("isCorrect").GetBoolean());
        await Assert.ThrowsAsync<TimeoutException>(() => atStudent);
    }
}
