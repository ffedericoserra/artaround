using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class VisitBlockQuestionConfiguration : IEntityTypeConfiguration<VisitBlockQuestion>
{
    public void Configure(EntityTypeBuilder<VisitBlockQuestion> builder)
    {
        builder.ToTable("visit_block_questions");
        builder.Property(q => q.Id).UseIdentityAlwaysColumn();
        builder.HasIndex(q => new { q.BlockId, q.Position }).IsUnique();
    }
}
