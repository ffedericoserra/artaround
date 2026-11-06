using System.Text.Json.Serialization;
using ArtAround.Api.Domain;

namespace ArtAround.Api.Dtos;

/// <summary>GET /auth/me: the user plus their visits. savedMuseums has no table yet.</summary>
public record ProfileDto(
    [property: JsonPropertyName("_id")] string Id,
    string Username,
    string Email,
    string? AvatarUrl,
    string Language,
    int WalletBalance,
    List<VisitSummaryDto> MyVisits,
    List<VisitSummaryDto> SavedVisits,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ProfileDto From(User u, IEnumerable<Visit> myVisits, IEnumerable<Visit> savedVisits) =>
        new(u.Id.ToString(), u.Username, u.Email, u.AvatarUrl, u.Language.ToString().ToLowerInvariant(), u.WalletBalance,
            myVisits.Select(VisitSummaryDto.From).ToList(), savedVisits.Select(VisitSummaryDto.From).ToList(),
            u.CreatedAt, u.UpdatedAt);
}
