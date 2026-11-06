using System.Text.Json.Serialization;
using ArtAround.Api.Domain;

namespace ArtAround.Api.Dtos;

public record ContentDto(
    [property: JsonPropertyName("_id")] string Id,
    string UniversalId,
    string MuseumId,
    string Type,
    string Name,
    string? Author,
    string? Year,
    string? ImageUrl,
    CoordinatesDto Coordinates,
    int Floor,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ContentDto From(Content c) =>
        new(
            c.Id.ToString(), c.UniversalId, c.MuseumId.ToString(), c.Type.ToString(), c.Name,
            c.Author, c.Year, c.ImageUrl,
            new CoordinatesDto(c.Lat, c.Lng), c.Floor,
            c.CreatedAt, c.UpdatedAt);
}
