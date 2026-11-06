using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("items", t => t.HasCheckConstraint("items_price_nonnegative", "price >= 0"));
        builder.Property(i => i.Id).UseIdentityAlwaysColumn();
        builder.Property(i => i.TargetAudience).HasMaxLength(20);
        builder.Property(i => i.Price).HasDefaultValue(0);
        builder.Property(i => i.License).HasDefaultValue(License.CcBy);
        builder.Property(i => i.IsPublic).HasDefaultValue(false);
        builder.Property(i => i.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(i => i.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.HasOne(i => i.Content).WithMany().HasForeignKey(i => i.ContentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Creator).WithMany().HasForeignKey(i => i.CreatorId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(i => i.Texts).WithOne().HasForeignKey(t => t.ItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(i => i.AssociatedContents).WithMany()
            .UsingEntity(
                "item_associated_contents",
                right => right.HasOne(typeof(Content)).WithMany().HasForeignKey("content_id"),
                left => left.HasOne(typeof(Item)).WithMany().HasForeignKey("item_id"));
    }
}
