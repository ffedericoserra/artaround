using ArtAround.Api.Data;
using ArtAround.Api.Domain;
using ArtAround.Api.Dtos;
using ArtAround.Api.Hubs;
using ArtAround.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ArtAround.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionsController(AppDbContext db, IHubContext<SessionHub> hub) : ControllerBase
{
    private const string NotFoundMessage = "Session not found or inactive";

    // Only what a participant can trigger: joined and left are written by the server.
    private static readonly string[] ClientActions =
        ["more", "shorter", "complex", "simpler", "details", "map", "author", "year", "exit", "tellMore", "tellLess", "tooSimple"];

    private static readonly string[] CodeAdjectives = ["ROSSO", "BLU", "VERDE", "GIALLO", "VIOLA", "ARANCIO"];
    private static readonly string[] CodeNouns = ["LEONE", "AQUILA", "TIGRE", "LUPO", "FALCO", "ORSO"];

    /* ---------- lifecycle ---------- */

    [HttpPost]
    public async Task<IActionResult> Create(CreateSessionRequest request)
    {
        if (!long.TryParse(request.VisitId, out var visitId))
        {
            return ApiProblem.Validation([new("visitId", "Visit ID is required")]);
        }
        var customCode = request.Code?.Trim().ToUpperInvariant();
        if (customCode is { Length: < 3 or > 50 })
        {
            return ApiProblem.Validation([new("code", "Code must be between 3 and 50 characters")]);
        }

        var visit = await db.Visits.AsNoTracking().FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit is null)
        {
            return ApiProblem.NotFound("Visit not found");
        }
        if (visit.Type != VisitType.Synchronized)
        {
            return ApiProblem.BadRequest("Visit is not synchronized");
        }

        var userId = User.GetId();
        if (!visit.IsPublic && visit.CreatorId != userId)
        {
            return ApiProblem.Forbidden("Not authorized to host this visit");
        }

        var code = customCode ?? await GenerateCodeAsync();
        if (await db.Sessions.AnyAsync(s => s.Code == code && s.IsActive))
        {
            return ApiProblem.Conflict("Session code already in use");
        }

        var session = new Session { Code = code, OwnerId = userId, VisitId = visitId };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new { session = await ReadAsync(session.Id) });
    }

    [HttpGet("my")]
    public async Task<IActionResult> My()
    {
        var userId = User.GetId();
        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => s.OwnerId == userId)
            .OrderByDescending(s => s.Id)
            .Take(20)
            .Select(s => new
            {
                _id = s.Id.ToString(),
                s.Code,
                visitId = new { _id = s.VisitId.ToString(), title = s.Visit.Title },
                s.IsActive,
                s.StartedAt,
                s.EndedAt,
            })
            .ToListAsync();

        return Ok(new { sessions });
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> Get(string code)
    {
        var id = await ActiveIdAsync(code);
        return id is null ? ApiProblem.NotFound(NotFoundMessage) : Ok(new { session = await ReadAsync(id.Value) });
    }

    [HttpPost("{code}/join")]
    public async Task<IActionResult> Join(string code)
    {
        var session = await Active(code).Include(s => s.Participants).FirstOrDefaultAsync();
        if (session is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }

        var userId = User.GetId();
        var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is null)
        {
            session.Participants.Add(new SessionParticipant { UserId = userId });
            session.Activities.Add(new SessionActivity { UserId = userId, Action = "joined" });
        }
        else
        {
            participant.IsActive = true;
        }
        await db.SaveChangesAsync();

        await BroadcastParticipantsAsync(session);
        return Ok(new { session = await ReadAsync(session.Id) });
    }

    [HttpPost("{code}/leave")]
    public async Task<IActionResult> Leave(string code)
    {
        var session = await Active(code).Include(s => s.Participants).FirstOrDefaultAsync();
        if (session is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }

        var userId = User.GetId();
        var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is not null)
        {
            participant.IsActive = false;
            session.Activities.Add(new SessionActivity { UserId = userId, Action = "left" });
            await db.SaveChangesAsync();
            await BroadcastParticipantsAsync(session);
        }

        return Ok(new { message = "Left session" });
    }

    [HttpPost("{code}/end")]
    public async Task<IActionResult> End(string code)
    {
        var session = await Active(code).FirstOrDefaultAsync();
        if (session is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }
        if (session.OwnerId != User.GetId())
        {
            return ApiProblem.Forbidden("Only session owner can end");
        }

        session.IsActive = false;
        session.EndedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await Room(session).SendAsync("session:ended", new { });
        return Ok(new { message = "Session ended" });
    }

    /* ---------- navigation ---------- */

    [HttpPost("{code}/advance")]
    public Task<IActionResult> Advance(string code) => MoveAsync(code, +1);

    [HttpPost("{code}/previous")]
    public Task<IActionResult> Previous(string code) => MoveAsync(code, -1);

    private async Task<IActionResult> MoveAsync(string code, int direction)
    {
        var session = await Active(code)
            .Include(s => s.Visit).ThenInclude(v => v.Blocks).ThenInclude(b => b.Items)
            .Include(s => s.Visit).ThenInclude(v => v.Blocks).ThenInclude(b => b.Questions)
            .AsSplitQuery()
            .FirstOrDefaultAsync();
        if (session is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }
        if (session.OwnerId != User.GetId())
        {
            return ApiProblem.Forbidden("Only session owner can navigate");
        }

        var steps = SessionStep.From(session.Visit);
        var target = session.CurrentStepIndex + direction;
        if (target >= 0 && target < steps.Count)
        {
            session.CurrentStepIndex = target;
            session.CurrentItemIndex = steps[target].ItemIndex ?? session.CurrentItemIndex;
            await db.SaveChangesAsync();
        }

        var step = steps.ElementAtOrDefault(session.CurrentStepIndex);
        var position = new
        {
            session.CurrentItemIndex,
            session.CurrentStepIndex,
            step,
            isFirst = session.CurrentStepIndex == 0,
            isLast = session.CurrentStepIndex >= steps.Count - 1,
        };

        await Room(session).SendAsync("session:step-changed", position);
        if (step?.Type == "artwork")
        {
            await Room(session).SendAsync("session:item-changed", new { position.CurrentItemIndex, position.isFirst, position.isLast });
        }

        return Ok(position);
    }

    /* ---------- activity and chat ---------- */

    [HttpPost("{code}/activity")]
    public async Task<IActionResult> LogActivity(string code, LogActivityRequest request)
    {
        if (request.Action is null || !ClientActions.Contains(request.Action))
        {
            return ApiProblem.Validation([new("action", "Invalid action")]);
        }

        var session = await Active(code).FirstOrDefaultAsync();
        if (session is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }

        var userId = User.GetId();
        if (!await IsMemberAsync(session, userId))
        {
            return ApiProblem.Forbidden("You are not in this session");
        }

        var username = await UsernameAsync(userId);
        var activity = new SessionActivity { SessionId = session.Id, UserId = userId, Action = request.Action };
        db.SessionActivities.Add(activity);
        await db.SaveChangesAsync();

        await Room(session).SendAsync("session:activity", new ActivityDto(userId.ToString(), activity.Action, activity.Timestamp, username));
        return Ok(new { message = "Activity logged" });
    }

    [HttpPost("{code}/message")]
    public async Task<IActionResult> SendMessage(string code, SendMessageRequest request)
    {
        var text = request.Text?.Trim();
        if (text is null or { Length: 0 or > 500 })
        {
            return ApiProblem.Validation([new("text", "Message must be between 1 and 500 characters")]);
        }

        var session = await Active(code).FirstOrDefaultAsync();
        if (session is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }

        var userId = User.GetId();
        if (!await IsMemberAsync(session, userId))
        {
            return ApiProblem.Forbidden("You are not in this session");
        }

        var username = await UsernameAsync(userId);
        var message = new SessionMessage { SessionId = session.Id, UserId = userId, Text = text };
        db.SessionMessages.Add(message);
        await db.SaveChangesAsync();

        // To the whole room including the sender, so every client appends it the same way.
        var dto = new MessageDto(userId.ToString(), username, message.Text, message.Timestamp);
        await Room(session).SendAsync("session:chat", dto);
        return StatusCode(StatusCodes.Status201Created, new { message = dto });
    }

    /* ---------- question sections ---------- */

    [HttpPost("{code}/sections/{sectionId}/answers")]
    public async Task<IActionResult> SubmitAnswer(string code, string sectionId, SubmitAnswerRequest request)
    {
        if (!long.TryParse(request.QuestionId, out var questionId))
        {
            return ApiProblem.Validation([new("questionId", "Question ID is required")]);
        }

        var session = await Active(code)
            .Include(s => s.Participants)
            .Include(s => s.Visit).ThenInclude(v => v.Blocks).ThenInclude(b => b.Items)
            .Include(s => s.Visit).ThenInclude(v => v.Blocks).ThenInclude(b => b.Questions)
            .AsSplitQuery()
            .FirstOrDefaultAsync();
        if (session is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }

        var userId = User.GetId();
        if (!session.Participants.Any(p => p.UserId == userId && p.IsActive))
        {
            return ApiProblem.Forbidden("Only active participants can answer");
        }

        var currentStep = SessionStep.From(session.Visit).ElementAtOrDefault(session.CurrentStepIndex);
        if (currentStep?.Type != "questions" || currentStep.SectionId != sectionId)
        {
            return ApiProblem.Conflict("This question section is not active");
        }

        var section = session.Visit.Blocks.First(b => b.Id.ToString() == sectionId);
        var question = section.Questions.FirstOrDefault(q => q.Id == questionId);
        if (question is null)
        {
            return ApiProblem.NotFound("Question not found");
        }

        var text = request.Text?.Trim();
        bool? isCorrect = null;
        if (question.AnswerType == AnswerType.MultipleChoice)
        {
            if (request.SelectedIndex is not >= 0 || request.SelectedIndex >= question.Options.Count)
            {
                return ApiProblem.BadRequest("Select a valid option");
            }
            isCorrect = request.SelectedIndex == question.CorrectIndex;
            text = null;
        }
        else if (string.IsNullOrEmpty(text) || text.Length > 1000)
        {
            return ApiProblem.BadRequest("Write an answer of at most 1000 characters");
        }

        var response = await db.SectionResponses.FindAsync(session.Id, questionId, userId);
        var isNew = response is null;
        if (response is null)
        {
            response = new SectionResponse { SessionId = session.Id, QuestionId = questionId, UserId = userId, SectionId = section.Id, AnswerType = question.AnswerType };
            db.SectionResponses.Add(response);
        }
        response.Text = text;
        response.SelectedIndex = question.AnswerType == AnswerType.MultipleChoice ? request.SelectedIndex : null;
        response.IsCorrect = isCorrect;
        response.SubmittedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        response.User = await db.Users.FirstAsync(u => u.Id == userId);
        await hub.Clients.Group(SessionHub.OwnerGroup(session.Code)).SendAsync("session:section-response", SectionResponseDto.From(response, showResult: true));

        var body = new { response = SectionResponseDto.From(response, showResult: false) };
        return isNew ? StatusCode(StatusCodes.Status201Created, body) : Ok(body);
    }

    [HttpGet("{code}/sections/{sectionId}/responses")]
    public async Task<IActionResult> GetResponses(string code, string sectionId)
    {
        if (!long.TryParse(sectionId, out var section))
        {
            return ApiProblem.BadRequest("Invalid id format");
        }

        var session = await Latest(code).Select(s => new { s.Id, s.OwnerId }).FirstOrDefaultAsync();
        if (session is null)
        {
            return ApiProblem.NotFound("Session not found");
        }
        if (session.OwnerId != User.GetId())
        {
            return ApiProblem.Forbidden("Only the session owner can view responses");
        }

        var responses = await db.SectionResponses.AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.SessionId == session.Id && r.SectionId == section)
            .ToListAsync();
        return Ok(new { responses = responses.Select(r => SectionResponseDto.From(r, showResult: true)).ToList() });
    }

    /* ---------- quiz ---------- */

    [HttpPost("{code}/quiz/start")]
    public async Task<IActionResult> StartQuiz(string code)
    {
        var session = await Active(code).Include(s => s.Visit).ThenInclude(v => v.Quiz).FirstOrDefaultAsync();
        if (session is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }
        if (session.OwnerId != User.GetId())
        {
            return ApiProblem.Forbidden("Only session owner can start the quiz");
        }
        if (session.Visit.Quiz.Count == 0)
        {
            return ApiProblem.BadRequest("This visit has no quiz");
        }

        session.QuizStarted = true;
        await db.SaveChangesAsync();

        await Room(session).SendAsync("session:quiz-started", new { });
        return Ok(new { message = "Quiz started" });
    }

    [HttpPost("{code}/quiz")]
    public async Task<IActionResult> SubmitQuiz(string code, SubmitQuizRequest request)
    {
        var answers = request.Answers;
        if (answers is null or { Count: 0 }
            || answers.Any(a => a.QuestionIndex < 0 || a.SelectedIndex < 0)
            || answers.Select(a => a.QuestionIndex).Distinct().Count() != answers.Count)
        {
            return ApiProblem.Validation([new("answers", "One answer per question is required")]);
        }

        var session = await Active(code)
            .Include(s => s.Visit).ThenInclude(v => v.Quiz)
            .Include(s => s.Participants).ThenInclude(p => p.User)
            .FirstOrDefaultAsync();
        if (session is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }
        if (session.Visit.Quiz.Count == 0)
        {
            return ApiProblem.BadRequest("This session has no quiz");
        }

        var userId = User.GetId();
        var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is null)
        {
            return ApiProblem.Forbidden("You are not a participant of this session");
        }
        if (participant.QuizScore is not null)
        {
            return ApiProblem.BadRequest("Quiz already submitted");
        }

        // Scored here, against the key the students never receive.
        var quiz = session.Visit.Quiz.OrderBy(q => q.Position).ToList();
        participant.QuizScore = answers.Count(a => a.QuestionIndex < quiz.Count && quiz[a.QuestionIndex].CorrectIndex == a.SelectedIndex);
        participant.QuizAnswers = answers
            .Select(a => new SessionQuizAnswer { QuestionIndex = a.QuestionIndex, SelectedIndex = a.SelectedIndex })
            .ToList();
        await db.SaveChangesAsync();

        // Only the score travels: the answers stay behind GET /quiz, which is host-only.
        await Room(session).SendAsync("session:quiz-submitted", new
        {
            userId = userId.ToString(),
            username = participant.User.Username,
            quizScore = participant.QuizScore,
            total = quiz.Count,
        });

        return Ok(new { score = participant.QuizScore, total = quiz.Count });
    }

    [HttpGet("{code}/quiz")]
    public async Task<IActionResult> GetQuizResults(string code)
    {
        var session = await Latest(code).AsNoTracking()
            .Include(s => s.Visit).ThenInclude(v => v.Quiz)
            .Include(s => s.Participants).ThenInclude(p => p.User)
            .Include(s => s.Participants).ThenInclude(p => p.QuizAnswers)
            .AsSplitQuery()
            .FirstOrDefaultAsync();
        if (session is null)
        {
            return ApiProblem.NotFound("Session not found");
        }
        if (session.OwnerId != User.GetId())
        {
            return ApiProblem.Forbidden("Only session owner can view quiz results");
        }

        var results = session.Participants.Where(p => p.QuizScore is not null).Select(p => new
        {
            userId = p.UserId.ToString(),
            username = p.User.Username,
            quizAnswers = p.QuizAnswers.OrderBy(a => a.QuestionIndex).Select(a => new QuizAnswerDto(a.QuestionIndex, a.SelectedIndex)).ToList(),
            quizScore = p.QuizScore,
            total = session.Visit.Quiz.Count,
        }).ToList();

        return Ok(new { results, quiz = VisitMapping.Quiz(session.Visit, showAnswers: true) });
    }

    /* ---------- helpers ---------- */

    private IQueryable<Session> Active(string code)
    {
        var upperCode = code.ToUpperInvariant();
        return db.Sessions.Where(s => s.Code == upperCode && s.IsActive);
    }

    /// <summary>The newest session with this code, ended or not: hosts review results after the end.</summary>
    private IQueryable<Session> Latest(string code)
    {
        var upperCode = code.ToUpperInvariant();
        return db.Sessions.Where(s => s.Code == upperCode).OrderByDescending(s => s.Id);
    }

    private Task<long?> ActiveIdAsync(string code) => Active(code).Select(s => (long?)s.Id).FirstOrDefaultAsync();

    private async Task<SessionDto> ReadAsync(long id)
    {
        var session = await db.Sessions.AsNoTracking().WithEverything().FirstAsync(s => s.Id == id);
        return SessionDto.From(session, User.GetId());
    }

    private async Task<bool> IsMemberAsync(Session session, long userId) =>
        session.OwnerId == userId || await db.SessionParticipants.AnyAsync(p => p.SessionId == session.Id && p.UserId == userId);

    private Task<string> UsernameAsync(long userId) => db.Users.Where(u => u.Id == userId).Select(u => u.Username).FirstAsync();

    private IClientProxy Room(Session session) => hub.Clients.Group(SessionHub.Group(session.Code));

    private async Task BroadcastParticipantsAsync(Session session)
    {
        var participants = await db.SessionParticipants.AsNoTracking()
            .Include(p => p.User)
            .Where(p => p.SessionId == session.Id)
            .ToListAsync();
        await Room(session).SendAsync("session:participants", new { participants = ParticipantDto.ForRoom(participants) });
    }

    private async Task<string> GenerateCodeAsync()
    {
        string code;
        do
        {
            code = $"{Random.Shared.GetItems(CodeAdjectives, 1)[0]}_{Random.Shared.GetItems(CodeNouns, 1)[0]}_{Random.Shared.Next(100)}";
        }
        while (await db.Sessions.AnyAsync(s => s.Code == code && s.IsActive));

        return code;
    }
}
