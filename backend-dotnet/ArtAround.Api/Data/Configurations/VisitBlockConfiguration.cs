using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class VisitBlockConfiguration : IEntityTypeConfiguration<VisitBlock>
{
    public void Configure(EntityTypeBuilder<VisitBlock> builder)
    {
        builder.ToTable("visit_blocks");
        builder.Property(b => b.Id).UseIdentityAlwaysColumn();
        builder.HasIndex(b => new { b.VisitId, b.Position }).IsUnique();
        builder.HasMany(b => b.Items).WithOne().HasForeignKey(i => i.BlockId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Questions).WithOne().HasForeignKey(q => q.BlockId).OnDelete(DeleteBehavior.Cascade);
    }
}
