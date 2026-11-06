using ArtAround.Api.Data;
using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtAround.Api.Repositories;

public class EfMuseumRepository(AppDbContext db) : IMuseumRepository
{
    public async Task<IReadOnlyList<Museum>> ListAsync()
    {   
        var query = db.Museums.AsNoTracking();
        var sorted = await query.OrderBy(m => EF.Functions.Collate(m.Name, "C")).ToListAsync();
        
        return sorted;
    }

    public async Task<Museum?> FindAsync(string idOrSlug)
    {
        var query = db.Museums
            .Include(m => m.FloorPlans)
            .Include(m => m.PointsOfInterest)
            .AsNoTracking()
            .AsSplitQuery();

        return long.TryParse(idOrSlug, out var id)
            ? await query.FirstOrDefaultAsync(m => m.Id == id)
            : await query.FirstOrDefaultAsync(m => m.Slug == idOrSlug);
    }

    public async Task<IReadOnlyList<Content>?> ListContentsAsync(string idOrSlug, ContentType? type)
    {
        var museumId = await FindIdAsync(idOrSlug);
        if (museumId is null) return null;

        var query = db.Contents.AsNoTracking().Where(c => c.MuseumId == museumId);
        if (type is not null)
        {
            query = query.Where(c => c.Type == type);
        }

        return await query
            .OrderBy(c => EF.Functions.Collate(c.Name, "C"))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Visit>?> ListVisitsAsync(string idOrSlug, long? viewerId)
    {
        var museumId = await FindIdAsync(idOrSlug);
        if (museumId is null) return null;

        return await db.Visits.AsNoTracking()
            .Where(v => v.MuseumId == museumId && (v.IsPublic || v.CreatorId == viewerId))
            .WithSummary()
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    private Task<long?> FindIdAsync(string idOrSlug)
    {
        var query = long.TryParse(idOrSlug, out var id)
            ? db.Museums.Where(m => m.Id == id)
            : db.Museums.Where(m => m.Slug == idOrSlug);

        return query.Select(m => (long?)m.Id).FirstOrDefaultAsync();
    }
}
