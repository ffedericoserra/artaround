using System.Text.Json.Serialization;
using ArtAround.Api.Domain;
using ArtAround.Api.Infrastructure;

namespace ArtAround.Api.Dtos;

/// <summary>One position in a session: an artwork, or a whole question section.</summary>
public record SessionStep(
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ItemIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SectionId)
{
    /// <summary>The order the host walks through: each artwork alone, each question section as one step.</summary>
    public static List<SessionStep> From(Visit visit)
    {
        var steps = new List<SessionStep>();
        var itemIndex = 0;

        foreach (var block in visit.Blocks.OrderBy(b => b.Position))
        {
            if (block.Type == BlockType.Questions)
            {
                if (block.Questions.Count > 0)
                {
                    steps.Add(new("questions", null, block.Id.ToString()));
                }
                continue;
            }

            foreach (var _ in block.Items)
            {
                steps.Add(new("artwork", itemIndex, null));
                itemIndex++;
            }
        }

        return steps;
    }
}

public record QuizAnswerDto(int QuestionIndex, int SelectedIndex);

public record ParticipantDto(
    string UserId,
    string Username,
    DateTimeOffset JoinedAt,
    bool IsActive,
    int? QuizScore,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<QuizAnswerDto>? QuizAnswers)
{
    public static ParticipantDto From(SessionParticipant p, bool showQuiz) =>
        new(p.UserId.ToString(), p.User.Username, p.JoinedAt, p.IsActive,
            showQuiz ? p.QuizScore : null,
            showQuiz ? p.QuizAnswers.Select(a => new QuizAnswerDto(a.QuestionIndex, a.SelectedIndex)).ToList() : null);

    /// <summary>What everyone in the room may see: never scores or answers.</summary>
    public static List<ParticipantDto> ForRoom(IEnumerable<SessionParticipant> participants) =>
        participants.OrderBy(p => p.JoinedAt).Select(p => From(p, showQuiz: false)).ToList();
}

public record ActivityDto(string ParticipantId, string Action, DateTimeOffset Timestamp, string Username)
{
    public static ActivityDto From(SessionActivity a) => new(a.UserId.ToString(), a.Action, a.Timestamp, a.User.Username);
}

public record MessageDto(string UserId, string Username, string Text, DateTimeOffset Timestamp)
{
    public static MessageDto From(SessionMessage m) => new(m.UserId.ToString(), m.User.Username, m.Text, m.Timestamp);
}

public record SectionResponseDto(
    string SectionId,
    string QuestionId,
    string UserId,
    string Username,
    string AnswerType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SelectedIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsCorrect,
    DateTimeOffset SubmittedAt)
{
    /// <summary>Students may change answers while a section is open, so they never see the result.</summary>
    public static SectionResponseDto From(SectionResponse r, bool showResult) =>
        new(r.SectionId.ToString(), r.QuestionId.ToString(), r.UserId.ToString(), r.User.Username,
            Labels.AnswerTypes.Of(r.AnswerType), r.Text, r.SelectedIndex, showResult ? r.IsCorrect : null, r.SubmittedAt);
}

/// <summary>The visit inside a session: the plan and its questions, without full items.</summary>
public record SessionVisitDto(
    [property: JsonPropertyName("_id")] string Id,
    string Title,
    string? Description,
    string Slug,
    MuseumRefDto MuseumId,
    List<StopDto<string>> Sequence,
    List<BlockDto> Blocks,
    List<QuizQuestionDto> Quiz)
{
    public static SessionVisitDto From(Visit v, bool showAnswers) =>
        new(v.Id.ToString(), v.Title, v.Description, v.Slug, MuseumRefDto.From(v.Museum),
            VisitMapping.Stops(v, stop => stop.ItemId.ToString()),
            VisitMapping.Blocks(v, showAnswers),
            VisitMapping.Quiz(v, showAnswers));
}

public record SessionDto(
    [property: JsonPropertyName("_id")] string Id,
    string Code,
    UserRefDto Owner,
    SessionVisitDto VisitId,
    List<ParticipantDto> Participants,
    int ParticipantCount,
    int CurrentItemIndex,
    int CurrentStepIndex,
    bool IsActive,
    bool QuizStarted,
    bool IsOwner,
    List<ActivityDto> Activities,
    List<MessageDto> Messages,
    List<SectionResponseDto> SectionResponses,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt)
{
    public static SessionDto From(Session s, long viewerId)
    {
        var isOwner = s.OwnerId == viewerId;
        var showAnswers = isOwner || s.Visit.CreatorId == viewerId;
        return new(
            s.Id.ToString(), s.Code, UserRefDto.From(s.Owner), SessionVisitDto.From(s.Visit, showAnswers),
            s.Participants.OrderBy(p => p.JoinedAt).Select(p => ParticipantDto.From(p, showQuiz: isOwner || p.UserId == viewerId)).ToList(),
            s.Participants.Count(p => p.IsActive),
            s.CurrentItemIndex, s.CurrentStepIndex, s.IsActive, s.QuizStarted, isOwner,
            s.Activities.OrderBy(a => a.Id).Select(ActivityDto.From).ToList(),
            s.Messages.OrderBy(m => m.Id).Select(MessageDto.From).ToList(),
            s.Responses.Where(r => isOwner || r.UserId == viewerId).Select(r => SectionResponseDto.From(r, showResult: isOwner)).ToList(),
            s.StartedAt, s.EndedAt);
    }
}

/// <summary>Everything a client needs after connecting, so a reload or late join catches up at once.</summary>
public record SessionStateDto(
    int CurrentItemIndex,
    int CurrentStepIndex,
    List<ParticipantDto> Participants,
    int ParticipantCount,
    List<MessageDto> Messages,
    List<ActivityDto> Activities,
    bool QuizStarted,
    List<SectionResponseDto> SectionResponses)
{
    public static SessionStateDto From(SessionDto s) =>
        new(s.CurrentItemIndex, s.CurrentStepIndex, s.Participants, s.ParticipantCount, s.Messages, s.Activities, s.QuizStarted, s.SectionResponses);
}

public record CreateSessionRequest(string? VisitId, string? Code);

public record LogActivityRequest(string? Action);

public record SendMessageRequest(string? Text);

public record SubmitAnswerRequest(string? QuestionId, int? SelectedIndex, string? Text);

public record SubmitQuizRequest(List<QuizAnswerDto>? Answers);
