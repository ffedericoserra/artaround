namespace ArtAround.Api.Domain;

public class VisitBlockItem
{
    public long BlockId { get; set; }
    public int Position { get; set; }
    public long ItemId { get; set; }
    public string NextDirections { get; set; } = "";
    public string PrevDirections { get; set; } = "";
    public string? OverrideImage { get; set; }

    public Item Item { get; set; } = null!;
}
