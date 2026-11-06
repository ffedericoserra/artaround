namespace ArtAround.Api.Domain;

public class ItemText
{
    public long ItemId { get; set; }
    public Tone Tone { get; set; }
    public string Text { get; set; } = "";
}
