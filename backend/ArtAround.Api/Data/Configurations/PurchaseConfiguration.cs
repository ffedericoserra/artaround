using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("purchases", t => t.HasCheckConstraint("purchases_price_paid_nonnegative", "price_paid >= 0"));
        builder.HasKey(p => new { p.UserId, p.ItemId });
        builder.Property(p => p.PurchasedAt).HasDefaultValueSql("now()");
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.NoAction);

        // Restrict: an item someone paid for can't be deleted.
        builder.HasOne<Item>().WithMany(i => i.Purchases).HasForeignKey(p => p.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
