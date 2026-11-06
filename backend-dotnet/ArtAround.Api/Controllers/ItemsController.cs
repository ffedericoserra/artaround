using ArtAround.Api.Authorization;
using ArtAround.Api.Data;
using ArtAround.Api.Domain;
using ArtAround.Api.Dtos;
using ArtAround.Api.Infrastructure;
using ArtAround.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArtAround.Api.Controllers;

[ApiController]
[Route("api/items")]
public class ItemsController(AppDbContext db, IAuthorizationService authorization, PurchaseService purchases) : ControllerBase
{
    private const string NotFoundMessage = "Item not found";
    private const string InvalidId = "Invalid id format";

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> List(string? contentId, string? creatorId, string? targetAudience, string? isPublic)
    {
        var viewerId = User.FindId();
        var query = db.Items.AsNoTracking();

        if (!string.IsNullOrEmpty(contentId))
        {
            query = query.Where(i => i.Content.UniversalId == contentId);
        }
        if (!string.IsNullOrEmpty(creatorId))
        {
            if (!long.TryParse(creatorId, out var creator))
            {
                return ApiProblem.BadRequest(InvalidId);
            }
            query = query.Where(i => i.CreatorId == creator);
        }
        if (!string.IsNullOrEmpty(targetAudience))
        {
            query = query.Where(i => i.TargetAudience == targetAudience);
        }

        query = isPublic switch
        {
            "true" => query.Where(i => i.IsPublic),
            not null => query.Where(i => !i.IsPublic && i.CreatorId == viewerId),
            null => query.Where(i => i.IsPublic || i.CreatorId == viewerId || i.Purchases.Any(p => p.UserId == viewerId)),
        };

        var items = await query.WithDetails().OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id).ToListAsync();
        return Ok(new { items = items.Select(i => ItemDto.From(i, viewerId)).ToList() });
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        if (!long.TryParse(id, out var itemId))
        {
            return ApiProblem.BadRequest(InvalidId);
        }

        var viewerId = User.FindId();
        var item = await db.Items.AsNoTracking().WithDetails().FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }

        // A public visit must be able to show the private items it uses.
        var canRead = item.IsPublic || item.CreatorId == viewerId || item.Purchases.Any(p => p.UserId == viewerId)
            || await db.Visits.AnyAsync(v => v.IsPublic && v.Blocks.Any(b => b.Items.Any(s => s.ItemId == itemId)));
        return canRead
            ? Ok(new { item = ItemDto.From(item, viewerId) })
            : ApiProblem.Forbidden("Not authorized to view this item");
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateItemRequest request)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return ApiProblem.Validation(errors);
        }

        var content = await db.Contents.FirstOrDefaultAsync(c => c.UniversalId == request.ContentId);
        if (content is null)
        {
            return ApiProblem.BadRequest("contentId must match an existing Content.universalId");
        }

        var item = new Item
        {
            ContentId = content.Id,
            CreatorId = User.GetId(),
            TargetAudience = request.TargetAudience!.Trim(),
            Price = request.Price ?? 0,
            License = request.License is null ? License.CcBy : Labels.Licenses.Parse(request.License),
            IsPublic = request.IsPublic ?? false,
            Texts = request.Texts!.Select(t => new ItemText { Tone = Labels.Tones.Parse(t.Tone!), Text = t.Text! }).ToList(),
        };

        var associated = await LoadAssociatedAsync(content, request.AssociatedContents);
        if (associated is null)
        {
            return ApiProblem.BadRequest("associatedContents must be other contents of the same museum");
        }
        item.AssociatedContents = associated;

        db.Items.Add(item);
        await db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new { item = await ReadAsync(item.Id) });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateItemRequest request)
    {
        if (!long.TryParse(id, out var itemId))
        {
            return ApiProblem.BadRequest(InvalidId);
        }

        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return ApiProblem.Validation(errors);
        }

        var item = await db.Items
            .Include(i => i.Content)
            .Include(i => i.Texts)
            .Include(i => i.AssociatedContents)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }
        if (!(await authorization.AuthorizeAsync(User, item, OwnerHandler.Policy)).Succeeded)
        {
            return ApiProblem.Forbidden("Not authorized to update this item");
        }

        if (!string.IsNullOrWhiteSpace(request.TargetAudience))
        {
            item.TargetAudience = request.TargetAudience.Trim();
        }
        if (request.Price is not null)
        {
            item.Price = request.Price.Value;
        }
        if (request.License is not null)
        {
            item.License = Labels.Licenses.Parse(request.License);
        }
        if (request.IsPublic is not null)
        {
            item.IsPublic = request.IsPublic.Value;
        }
        if (request.Texts is not null)
        {
            ReplaceTexts(item, request.Texts);
        }
        if (request.AssociatedContents is not null)
        {
            var associated = await LoadAssociatedAsync(item.Content, request.AssociatedContents);
            if (associated is null)
            {
                return ApiProblem.BadRequest("associatedContents must be other contents of the same museum");
            }
            item.AssociatedContents = associated;
        }

        await db.SaveChangesAsync();
        return Ok(new { item = await ReadAsync(item.Id) });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!long.TryParse(id, out var itemId))
        {
            return ApiProblem.BadRequest(InvalidId);
        }

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }
        if (!(await authorization.AuthorizeAsync(User, item, OwnerHandler.Policy)).Succeeded)
        {
            return ApiProblem.Forbidden("Not authorized to delete this item");
        }

        db.Items.Remove(item);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            return ApiProblem.Conflict("Items used by a visit or already sold cannot be deleted");
        }

        return Ok(new { message = "Item deleted" });
    }

    [HttpPost("{id}/purchase")]
    public async Task<IActionResult> Purchase(string id)
    {
        if (!long.TryParse(id, out var itemId))
        {
            return ApiProblem.BadRequest(InvalidId);
        }

        var buyerId = User.GetId();
        var creatorId = await db.Items.Where(i => i.Id == itemId).Select(i => (long?)i.CreatorId).FirstOrDefaultAsync();
        if (creatorId is null)
        {
            return ApiProblem.NotFound(NotFoundMessage);
        }
        if (creatorId == buyerId)
        {
            return ApiProblem.BadRequest("Creators already own their items");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await purchases.AdoptAsync(buyerId, [itemId]);
        await transaction.CommitAsync();

        return Ok(new
        {
            message = result.AdoptedItemIds.Count > 0 ? "Item purchased successfully" : "Item already purchased",
            result.ChargedAmount,
            result.WalletBalance,
            result.AdoptedItemIds,
        });
    }

    private async Task<ItemDto> ReadAsync(long id)
    {
        var item = await db.Items.AsNoTracking().WithDetails().FirstAsync(i => i.Id == id);
        return ItemDto.From(item, User.FindId());
    }

    /// <summary>The primary key is (item, tone), so a tone that stays is updated, not deleted and reinserted.</summary>
    private static void ReplaceTexts(Item item, List<ItemTextRequest> texts)
    {
        var requested = texts.ToDictionary(t => Labels.Tones.Parse(t.Tone!), t => t.Text!);

        item.Texts.RemoveAll(t => !requested.ContainsKey(t.Tone));
        foreach (var (tone, text) in requested)
        {
            var existing = item.Texts.FirstOrDefault(t => t.Tone == tone);
            if (existing is null)
            {
                item.Texts.Add(new ItemText { Tone = tone, Text = text });
            }
            else
            {
                existing.Text = text;
            }
        }
    }

    /// <summary>The contents to associate, or null if any id isn't another content of the same museum.</summary>
    private async Task<List<Content>?> LoadAssociatedAsync(Content main, List<string>? ids)
    {
        if (ids is null or { Count: 0 })
        {
            return [];
        }

        var parsed = new HashSet<long>();
        foreach (var id in ids)
        {
            if (!long.TryParse(id, out var contentId) || contentId == main.Id)
            {
                return null;
            }
            parsed.Add(contentId);
        }

        var contents = await db.Contents.Where(c => parsed.Contains(c.Id) && c.MuseumId == main.MuseumId).ToListAsync();
        return contents.Count == parsed.Count ? contents : null;
    }
}
