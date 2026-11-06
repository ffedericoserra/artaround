namespace ArtAround.Api.Domain;

public class SessionMessage
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public long UserId { get; set; }
    public string Text { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
}
