namespace ArtAround.Api.Domain;

public class Item : IOwned
{
    public long Id { get; set; }
    public long ContentId { get; set; }
    public long CreatorId { get; set; }
    public string TargetAudience { get; set; } = "";
    public int Price { get; set; }
    public License License { get; set; }
    public bool IsPublic { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Postgres's xmin: SaveChanges fails if the row changed since it was read.</summary>
    public uint Version { get; set; }

    public Content Content { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public List<ItemText> Texts { get; set; } = [];
    public List<Content> AssociatedContents { get; set; } = [];
    public List<Purchase> Purchases { get; set; } = [];
}
