namespace ArtAround.Api.Domain;

public class VisitBlockQuestion
{
    public long Id { get; set; }
    public long BlockId { get; set; }
    public int Position { get; set; }
    public string Prompt { get; set; } = "";
    public AnswerType AnswerType { get; set; }
    public List<string> Options { get; set; } = [];
    public int? CorrectIndex { get; set; }
}
