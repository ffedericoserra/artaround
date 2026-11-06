using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ArtAround.Api.Infrastructure;

public static class UserExtensions
{
    /// <summary>The caller's id, or null when the request is anonymous.</summary>
    public static long? FindId(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    /// <summary>The caller's id, for actions that require a token.</summary>
    public static long GetId(this ClaimsPrincipal user) => user.FindId()!.Value;
}
