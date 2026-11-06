using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtAround.Api.Data;

public static class QueryExtensions
{
    public static IQueryable<Item> WithDetails(this IQueryable<Item> items) => items
        .AsSplitQuery()
        .Include(i => i.Content)
        .Include(i => i.Creator)
        .Include(i => i.Texts)
        .Include(i => i.AssociatedContents);

    /// <summary>Visit lists: refs and each stop's content id, no full items.</summary>
    public static IQueryable<Visit> WithSummary(this IQueryable<Visit> visits) => visits
        .AsSplitQuery()
        .Include(v => v.Museum)
        .Include(v => v.Creator)
        .Include(v => v.Blocks).ThenInclude(b => b.Items).ThenInclude(i => i.Item).ThenInclude(i => i.Content);

    /// <summary>Everything needed to run a visit.</summary>
    public static IQueryable<Visit> WithDetails(this IQueryable<Visit> visits) => visits
        .AsSplitQuery()
        .Include(v => v.Museum).ThenInclude(m => m.FloorPlans)
        .Include(v => v.Museum).ThenInclude(m => m.PointsOfInterest)
        .Include(v => v.Creator)
        .Include(v => v.Quiz)
        .Include(v => v.Blocks).ThenInclude(b => b.Questions)
        .Include(v => v.Blocks).ThenInclude(b => b.Items).ThenInclude(i => i.Item).ThenInclude(i => i.Content)
        .Include(v => v.Blocks).ThenInclude(b => b.Items).ThenInclude(i => i.Item).ThenInclude(i => i.Creator)
        .Include(v => v.Blocks).ThenInclude(b => b.Items).ThenInclude(i => i.Item).ThenInclude(i => i.Texts)
        .Include(v => v.Blocks).ThenInclude(b => b.Items).ThenInclude(i => i.Item).ThenInclude(i => i.AssociatedContents);
}
