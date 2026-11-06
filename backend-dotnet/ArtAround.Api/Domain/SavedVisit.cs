namespace ArtAround.Api.Domain;

public class SavedVisit
{
    public long UserId { get; set; }
    public long VisitId { get; set; }

    public Visit Visit { get; set; } = null!;
}
