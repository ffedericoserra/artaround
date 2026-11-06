using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class SavedVisitConfiguration : IEntityTypeConfiguration<SavedVisit>
{
    public void Configure(EntityTypeBuilder<SavedVisit> builder)
    {
        builder.ToTable("saved_visits");
        builder.HasKey(s => new { s.UserId, s.VisitId });
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Visit).WithMany().HasForeignKey(s => s.VisitId).OnDelete(DeleteBehavior.Cascade);
    }
}
