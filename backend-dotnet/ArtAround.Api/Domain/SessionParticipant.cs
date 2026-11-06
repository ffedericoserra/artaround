namespace ArtAround.Api.Domain;

public class SessionParticipant
{
    public long SessionId { get; set; }
    public long UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
    public int? QuizScore { get; set; }

    public User User { get; set; } = null!;
    public List<SessionQuizAnswer> QuizAnswers { get; set; } = [];
}
