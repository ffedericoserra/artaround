using System.Text.Json.Serialization;
using ArtAround.Api.Domain;

namespace ArtAround.Api.Dtos;

public record MuseumDto(
    [property: JsonPropertyName("_id")] string Id,
    string Name,
    string Slug,
    string? Address,
    string? Description,
    string? Website,
    string? Email,
    string? Phone,
    string? ImageUrl,
    IReadOnlyList<FloorPlanDto> FloorPlans,
    IReadOnlyList<PointOfInterestDto> PointsOfInterest,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static MuseumDto From(Museum m) =>
        new(
            m.Id.ToString(), m.Name, m.Slug, m.Address, m.Description,
            m.Website, m.Email, m.Phone, m.ImageUrl,
            m.FloorPlans.Select(FloorPlanDto.From).ToList(),
            m.PointsOfInterest.Select(PointOfInterestDto.From).ToList(),
            m.CreatedAt, m.UpdatedAt);
}
