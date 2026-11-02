using ArtAround.Api.Domain;

namespace ArtAround.Api.Dtos;

public record PointOfInterestDto(string Type, string Label, CoordinatesDto Coordinates, int Floor)
{
    public static PointOfInterestDto From(PointOfInterest p) =>
        new(p.Type, p.Label, new CoordinatesDto(p.Lat, p.Lng), p.Floor);
}
