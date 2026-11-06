namespace ArtAround.Api.Domain;

public class VisitBlock
{
    public long Id { get; set; }
    public long VisitId { get; set; }
    public int Position { get; set; }
    public BlockType Type { get; set; }
    public string Name { get; set; } = "";

    public List<VisitBlockItem> Items { get; set; } = [];
    public List<VisitBlockQuestion> Questions { get; set; } = [];
}
