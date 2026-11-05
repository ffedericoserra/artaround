using System.Text.Json.Serialization;
using ArtAround.Api.Domain;

namespace ArtAround.Api.Dtos;

public record UserDto(
    [property: JsonPropertyName("_id")] string Id,
    string Username,
    string Email,
    string? AvatarUrl,
    string Language,
    int WalletBalance,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static UserDto From(User u) =>
        new(u.Id.ToString(), u.Username, u.Email, u.AvatarUrl, u.Language.ToString().ToLowerInvariant(),
            u.WalletBalance, u.CreatedAt, u.UpdatedAt);
}
