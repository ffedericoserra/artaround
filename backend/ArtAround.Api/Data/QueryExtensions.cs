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
        .Include(i => i.AssociatedContents)
        .Include(i => i.Purchases);

    /// <summary>A whole live session, for reading it or catching a client up.</summary>
    public static IQueryable<Session> WithEverything(this IQueryable<Session> sessions) => sessions
        .AsSplitQuery()
        .Include(s => s.Owner)
        .Include(s => s.Visit).ThenInclude(v => v.Museum)
        .Include(s => s.Visit).ThenInclude(v => v.Quiz)
        .Include(s => s.Visit).ThenInclude(v => v.Blocks).ThenInclude(b => b.Items)
        .Include(s => s.Visit).ThenInclude(v => v.Blocks).ThenInclude(b => b.Questions)
        .Include(s => s.Participants).ThenInclude(p => p.User)
        .Include(s => s.Participants).ThenInclude(p => p.QuizAnswers)
        .Include(s => s.Activities).ThenInclude(a => a.User)
        .Include(s => s.Messages).ThenInclude(m => m.User)
        .Include(s => s.Responses).ThenInclude(r => r.User);

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
        .Include(v => v.Blocks).ThenInclude(b => b.Items).ThenInclude(i => i.Item).ThenInclude(i => i.AssociatedContents)
        .Include(v => v.Blocks).ThenInclude(b => b.Items).ThenInclude(i => i.Item).ThenInclude(i => i.Purchases);
}
