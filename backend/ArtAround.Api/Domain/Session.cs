namespace ArtAround.Api.Domain;

/// <summary>A live group visit: a host moves through a visit and the participants follow.</summary>
public class Session
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public long OwnerId { get; set; }
    public long VisitId { get; set; }
    public int CurrentItemIndex { get; set; }
    public int CurrentStepIndex { get; set; }
    public bool IsActive { get; set; } = true;
    public bool QuizStarted { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Postgres's xmin: two hosts' clicks can't both move from the same position.</summary>
    public uint Version { get; set; }

    public User Owner { get; set; } = null!;
    public Visit Visit { get; set; } = null!;
    public List<SessionParticipant> Participants { get; set; } = [];
    public List<SessionActivity> Activities { get; set; } = [];
    public List<SessionMessage> Messages { get; set; } = [];
    public List<SectionResponse> Responses { get; set; } = [];
}
