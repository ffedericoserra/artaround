using ArtAround.Api.Authorization;
using ArtAround.Api.Data;
using ArtAround.Api.Domain;
using ArtAround.Api.Dtos;
using ArtAround.Api.Infrastructure;
using ArtAround.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtAround.Api.Controllers;

[ApiController]
[Route("api/visits")]
public class VisitsController(AppDbContext db, IAuthorizationService authorization, PurchaseService purchases) : ControllerBase
{
    private const string NotFoundMessage = "Visit not found";
    private const string InvalidId = "Invalid id format";
    private const string DefaultBlockName = "Mainboard";

    [HttpGet("my")]
    public async Task<IActionResult> My(string? museumId)
    {
        var userId = User.GetId();
        var query = db.Visits.AsNoTracking().Where(v => v.CreatorId == userId);
        if (!string.IsNullOrEmpty(museumId))
        {
            if (!long.TryParse(museumId, out var museum))
            {
                return ApiProblem.BadRequest(InvalidId);
            }
            query = query.Where(v => v.MuseumId == museum);
        }

        var visits = await query.WithSummary().OrderByDescending(v => v.UpdatedAt).ToListAsync();
        return Ok(new { visits = visits.Select(VisitSummaryDto.From).ToList() });
    }

    // Anyone with the link can read a visit, even a private one: isPublic only controls listings.
    [AllowAnonymous]
    [HttpGet("{idOrSlug}")]
    public async Task<IActionResult> Get(string idOrSlug)
    {
        var query = db.Visits.AsNoTracking().WithDetails();
        var visit = long.TryParse(idOrSlug, out var id)
            ? await query.FirstOrDefaultAsync(v => v.Id == id)
            : await query.OrderBy(v => v.Id).FirstOrDefaultAsync(v => v.Slug == idOrSlug);

        return visit is null
            ? ApiProblem.NotFound(NotFoundMessage)
            : Ok(new { visit = VisitDetailDto.From(visit, User.FindId()) });
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveVisitRequest request)
    {
        var errors = request.ValidateCreate();
        if (errors.Count > 0)
        {
            return ApiProblem.Validation(errors);
        }

        var museumId = long.Parse(request.MuseumId!);
        if (!await db.Museums.AnyAsync(m => m.Id == museumId))
        {
            return ApiProblem.BadRequest("Museum not found");
        }
        var title = request.Title!.Trim();
        var visit = new Visit
        {
            Title = title,
            Slug = Slug.From(title),
            MuseumId = museumId,
            CreatorId = User.GetId(),
            Description = request.Description?.Trim(),
            ImageUrl = request.ImageUrl,
            Type = request.Type is null ? VisitType.Standard : Labels.VisitTypes.Parse(request.Type),
            Length = request.Length is null ? VisitLength.Normal : Labels.VisitLengths.Parse(request.Length),
            IsPublic = request.IsPublic ?? true,
            Blocks = BuildBlocks(request.Blocks, request.Sequence ?? []),
            Quiz = BuildQuiz(request.Quiz ?? []),
        };

        await using var transaction = await db.Database.BeginTransactionAsync();
        var purchase = await purchases.AdoptAsync(visit.CreatorId, ItemIds(request));
        db.Visits.Add(visit);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return StatusCode(StatusCodes.Status201Created, await WriteResponseAsync(visit, purchase));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, SaveVisitRequest request)
    {
        if (!long.TryParse(id, out var visitId))
        {
            return ApiProblem.BadRequest(InvalidId);
        }

        var errors = request.ValidateCommon();
        if (errors.Count > 0)
        {
            return ApiProblem.Validation(errors);
        }

        var visit = await db.Visits
            .AsSplitQuery()
            .Include(v => v.Quiz)
            .Include(v => v.Blocks).ThenInclude(b => b.Items)
            .Include(v => v.Blocks).ThenInclude(b => b.Questions)
            .FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }
        if (!(await authorization.AuthorizeAsync(User, visit, OwnerHandler.Policy)).Succeeded)
        {
            return ApiProblem.Forbidden("Not authorized to update this visit");
        }
        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            visit.Title = request.Title.Trim();
            visit.Slug = Slug.From(visit.Title);
        }
        if (request.Description is not null)
        {
            visit.Description = request.Description.Trim();
        }
        if (request.ImageUrl is not null)
        {
            visit.ImageUrl = request.ImageUrl;
        }
        if (request.Type is not null)
        {
            visit.Type = Labels.VisitTypes.Parse(request.Type);
        }
        if (request.Length is not null)
        {
            visit.Length = Labels.VisitLengths.Parse(request.Length);
        }
        if (request.IsPublic is not null)
        {
            visit.IsPublic = request.IsPublic.Value;
        }

        // Children are replaced whole: EF deletes the removed rows and inserts the new ones.
        if (request.Blocks is not null || request.Sequence is not null)
        {
            var stops = request.Sequence ?? CurrentStops(visit);
            var blocks = request.Blocks ?? KeepQuestionBlocks(visit);
            var rebuilt = BuildBlocks(blocks, stops);
            visit.Blocks.Clear();
            visit.Blocks.AddRange(rebuilt);
        }
        if (request.Quiz is not null)
        {
            visit.Quiz.Clear();
            visit.Quiz.AddRange(BuildQuiz(request.Quiz));
        }

        // Always update the visit row: it bumps updated_at and makes the xmin check catch
        // a concurrent save, even when only the children changed.
        db.Entry(visit).State = EntityState.Modified;

        await using var transaction = await db.Database.BeginTransactionAsync();
        var purchase = await purchases.AdoptAsync(visit.CreatorId, ItemIds(request));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(await WriteResponseAsync(visit, purchase));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!long.TryParse(id, out var visitId))
        {
            return ApiProblem.BadRequest(InvalidId);
        }

        var visit = await db.Visits.FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }
        if (!(await authorization.AuthorizeAsync(User, visit, OwnerHandler.Policy)).Succeeded)
        {
            return ApiProblem.Forbidden("Not authorized to delete this visit");
        }

        db.Visits.Remove(visit);
        await db.SaveChangesAsync();
        return Ok(new { message = "Visit deleted" });
    }

    private async Task<VisitWriteResponse> WriteResponseAsync(Visit saved, PurchaseResult purchase)
    {
        var visit = await db.Visits.AsNoTracking().WithDetails().FirstAsync(v => v.Id == saved.Id);
        return new VisitWriteResponse(VisitDetailDto.From(visit, saved.CreatorId), purchase.ChargedAmount, purchase.WalletBalance, purchase.AdoptedItemIds);
    }

    private static IEnumerable<long> ItemIds(SaveVisitRequest request) =>
        (request.Sequence ?? []).Select(s => s.ItemId!)
            .Concat((request.Blocks ?? []).SelectMany(b => b.Items ?? []))
            .Select(long.Parse);

    private static List<StopRequest> CurrentStops(Visit visit) =>
        visit.Blocks.OrderBy(b => b.Position)
            .SelectMany(b => b.Items.OrderBy(i => i.Position))
            .Select(i => new StopRequest(i.ItemId.ToString(), i.NextDirections, i.PrevDirections, i.OverrideImage))
            .ToList();

    private static List<BlockRequest> KeepQuestionBlocks(Visit visit) =>
        visit.Blocks.Where(b => b.Type == BlockType.Questions).OrderBy(b => b.Position)
            .Select(b => new BlockRequest(
                "questions", b.Name, null,
                b.Questions.OrderBy(q => q.Position)
                    .Select(q => new BlockQuestionRequest(q.Prompt, Labels.AnswerTypes.Of(q.AnswerType), q.Options, q.CorrectIndex))
                    .ToList()))
            .ToList();

    /// <summary>
    /// Clients send artwork blocks (bare item ids) and a flat sequence (the directions). One
    /// model stores both: each block item takes its directions from the first unused stop
    /// with the same item, and stops no block mentions go into a default first block.
    /// </summary>
    private static List<VisitBlock> BuildBlocks(List<BlockRequest>? requests, List<StopRequest> stops)
    {
        var unused = stops.ToList();
        var blocks = new List<VisitBlock>();

        foreach (var request in requests ?? [])
        {
            var type = request.Type is null ? BlockType.Artwork : Labels.BlockTypes.Parse(request.Type);
            var block = new VisitBlock { Type = type, Name = string.IsNullOrWhiteSpace(request.BlockName) ? DefaultBlockName : request.BlockName.Trim() };

            if (type == BlockType.Questions)
            {
                block.Questions = (request.Questions ?? []).Select((q, position) => new VisitBlockQuestion
                {
                    Position = position,
                    Prompt = q.Prompt!.Trim(),
                    AnswerType = Labels.AnswerTypes.Parse(q.AnswerType!),
                    Options = q.Options ?? [],
                    CorrectIndex = q.CorrectIndex,
                }).ToList();
            }
            else
            {
                block.Items = (request.Items ?? []).Select((itemId, position) =>
                {
                    var stop = unused.FirstOrDefault(s => s.ItemId == itemId);
                    if (stop is not null)
                    {
                        unused.Remove(stop);
                    }
                    return ToBlockItem(itemId, position, stop);
                }).ToList();
            }

            blocks.Add(block);
        }

        if (unused.Count > 0)
        {
            blocks.Insert(0, new VisitBlock
            {
                Type = BlockType.Artwork,
                Name = DefaultBlockName,
                Items = unused.Select((stop, position) => ToBlockItem(stop.ItemId!, position, stop)).ToList(),
            });
        }

        for (var position = 0; position < blocks.Count; position++)
        {
            blocks[position].Position = position;
        }

        return blocks;
    }

    private static VisitBlockItem ToBlockItem(string itemId, int position, StopRequest? stop) => new()
    {
        ItemId = long.Parse(itemId),
        Position = position,
        NextDirections = stop?.NextDirections ?? "",
        PrevDirections = stop?.PrevDirections ?? "",
        OverrideImage = stop?.OverrideImage,
    };

    private static List<VisitQuizQuestion> BuildQuiz(List<QuizQuestionRequest> quiz) =>
        quiz.Select((q, position) => new VisitQuizQuestion
        {
            Position = position,
            Question = q.Question!,
            Options = q.Options!,
            CorrectIndex = q.CorrectIndex!.Value,
        }).ToList();
}
