using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class SectionResponseConfiguration : IEntityTypeConfiguration<SectionResponse>
{
    public void Configure(EntityTypeBuilder<SectionResponse> builder)
    {
        builder.ToTable("section_responses");
        builder.HasKey(r => new { r.SessionId, r.QuestionId, r.UserId });
        builder.Property(r => r.Text).HasMaxLength(1000);
        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<VisitBlockQuestion>().WithMany().HasForeignKey(r => r.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}
