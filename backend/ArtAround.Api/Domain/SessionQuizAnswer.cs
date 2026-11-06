namespace ArtAround.Api.Domain;

public class SessionQuizAnswer
{
    public long SessionId { get; set; }
    public long UserId { get; set; }
    public int QuestionIndex { get; set; }
    public int SelectedIndex { get; set; }
}
