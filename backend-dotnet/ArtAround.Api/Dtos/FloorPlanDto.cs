using ArtAround.Api.Domain;

namespace ArtAround.Api.Dtos;

public record FloorPlanDto(int Floor, string? Label, string? ImageUrl, BoundsDto Bounds)
{
    public static FloorPlanDto From(FloorPlan f) =>
        new(f.Floor, f.Label, f.ImageUrl, new BoundsDto(f.North, f.South, f.East, f.West));
}
