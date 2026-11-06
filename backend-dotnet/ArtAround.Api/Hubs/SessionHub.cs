using ArtAround.Api.Data;
using ArtAround.Api.Dtos;
using ArtAround.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ArtAround.Api.Hubs;

/// <summary>
/// Broadcast only: clients join a session's group and listen. Every change goes through
/// SessionsController, so ownership and validation rules live in one place.
/// </summary>
[Authorize]
public class SessionHub(AppDbContext db) : Hub
{
    public static string Group(string code) => $"session:{code}";
    public static string OwnerGroup(string code) => $"session:{code}:owner";

    public async Task JoinSession(string code)
    {
        var upperCode = code.ToUpperInvariant();
        var session = await db.Sessions.AsNoTracking().WithEverything()
            .FirstOrDefaultAsync(s => s.Code == upperCode && s.IsActive);
        if (session is null)
        {
            await Clients.Caller.SendAsync("session:error", "Session not found");
            return;
        }

        var userId = Context.User!.GetId();
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(upperCode));
        if (session.OwnerId == userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, OwnerGroup(upperCode));
        }

        await Clients.Caller.SendAsync("session:state", SessionStateDto.From(SessionDto.From(session, userId)));
    }
}
