namespace ArtAround.Api.Domain;

public class SectionResponse
{
    public long SessionId { get; set; }
    public long QuestionId { get; set; }
    public long UserId { get; set; }
    public long SectionId { get; set; }
    public AnswerType AnswerType { get; set; }
    public string? Text { get; set; }
    public int? SelectedIndex { get; set; }
    public bool? IsCorrect { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
}
