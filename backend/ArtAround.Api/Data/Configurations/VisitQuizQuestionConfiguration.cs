using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class VisitQuizQuestionConfiguration : IEntityTypeConfiguration<VisitQuizQuestion>
{
    public void Configure(EntityTypeBuilder<VisitQuizQuestion> builder)
    {
        builder.ToTable("visit_quiz_questions");
        builder.Property(q => q.Id).UseIdentityAlwaysColumn();
        builder.HasIndex(q => new { q.VisitId, q.Position }).IsUnique();
    }
}
