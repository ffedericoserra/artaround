using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class VisitBlockItemConfiguration : IEntityTypeConfiguration<VisitBlockItem>
{
    public void Configure(EntityTypeBuilder<VisitBlockItem> builder)
    {
        builder.ToTable("visit_block_items");
        builder.HasKey(i => new { i.BlockId, i.Position });

        // Restrict: an item used by a visit can't be deleted.
        builder.HasOne(i => i.Item).WithMany().HasForeignKey(i => i.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
