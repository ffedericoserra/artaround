namespace ArtAround.Api.Domain;

public class VisitQuizQuestion
{
    public long Id { get; set; }
    public long VisitId { get; set; }
    public int Position { get; set; }
    public string Question { get; set; } = "";
    public List<string> Options { get; set; } = [];
    public int CorrectIndex { get; set; }
}
