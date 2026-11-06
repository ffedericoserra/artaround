namespace ArtAround.Api.Domain;

public class Purchase
{
    public long UserId { get; set; }
    public long ItemId { get; set; }
    public int PricePaid { get; set; }
    public DateTimeOffset PurchasedAt { get; set; }
}
