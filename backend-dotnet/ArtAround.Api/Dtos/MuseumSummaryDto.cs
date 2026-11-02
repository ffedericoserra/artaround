using System.Text.Json.Serialization;
using ArtAround.Api.Domain;

namespace ArtAround.Api.Dtos;

public record MuseumSummaryDto(
    [property: JsonPropertyName("_id")] string Id,
    string Name,
    string Slug,
    string? Address,
    string? Description,
    string? ImageUrl)
{
    public static MuseumSummaryDto From(Museum m) =>
        new(m.Id.ToString(), m.Name, m.Slug, m.Address, m.Description, m.ImageUrl);
}
