using ArtAround.Api.Data;
using ArtAround.Api.Domain;
using ArtAround.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ArtAround.Api.Services;

public record PurchaseResult(int ChargedAmount, int WalletBalance, List<string> AdoptedItemIds);

public class PurchaseService(AppDbContext db)
{
    /// <summary>
    /// Buys every item the buyer neither created nor already owns, and pays each creator.
    /// Runs inside the caller's transaction, so a visit and its charge commit or fail together.
    /// </summary>
    public async Task<PurchaseResult> AdoptAsync(long buyerId, IEnumerable<long> itemIds)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("AdoptAsync needs an open transaction: FOR UPDATE locks last until it ends.");
        }

        var ids = itemIds.Distinct().ToArray();
        var items = await db.Items.AsNoTracking().Where(i => ids.Contains(i.Id)).ToListAsync();
        if (items.Count != ids.Length)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "One or more items do not exist");
        }

        // Lock the buyer and every creator, always in id order: two users buying from each other
        // would otherwise take the same two locks in opposite orders and deadlock. A second purchase
        // by this buyer now waits here, then reads the balance and purchases this one committed.
        var userIds = items.Select(i => i.CreatorId).Append(buyerId).Distinct().ToArray();
        var users = await db.Users
            .FromSql($"SELECT * FROM users WHERE id = ANY({userIds}) ORDER BY id FOR UPDATE")
            .ToDictionaryAsync(u => u.Id);
        if (!users.TryGetValue(buyerId, out var buyer))
        {
            throw new ApiException(StatusCodes.Status404NotFound, "User not found");
        }

        var alreadyOwned = await db.Purchases
            .Where(p => p.UserId == buyerId && ids.Contains(p.ItemId))
            .Select(p => p.ItemId)
            .ToListAsync();
        var toBuy = items.Where(i => i.CreatorId != buyerId && !alreadyOwned.Contains(i.Id)).ToList();

        if (toBuy.Any(i => !i.IsPublic))
        {
            throw new ApiException(StatusCodes.Status403Forbidden, "A private item cannot be adopted");
        }

        var total = toBuy.Sum(i => i.Price);
        if (buyer.WalletBalance < total)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "Insufficient balance");
        }

        buyer.WalletBalance -= total;
        foreach (var item in toBuy)
        {
            users[item.CreatorId].WalletBalance += item.Price;
            db.Purchases.Add(new Purchase { UserId = buyerId, ItemId = item.Id, PricePaid = item.Price });
        }
        await db.SaveChangesAsync();

        return new PurchaseResult(total, buyer.WalletBalance, toBuy.Select(i => i.Id.ToString()).ToList());
    }
}
