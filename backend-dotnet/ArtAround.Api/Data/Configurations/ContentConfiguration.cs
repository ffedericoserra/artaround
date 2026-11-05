using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class ContentConfiguration : IEntityTypeConfiguration<Content>
{
    public void Configure(EntityTypeBuilder<Content> builder)
    {
        builder.ToTable("contents", t =>
        {
            t.HasCheckConstraint("contents_lat_range", "lat BETWEEN -90 AND 90");
            t.HasCheckConstraint("contents_lng_range", "lng BETWEEN -180 AND 180");
        });
        builder.Property(c => c.Id).UseIdentityAlwaysColumn();
        builder.HasIndex(c => c.UniversalId).IsUnique();
        builder.HasIndex(c => new { c.MuseumId, c.Type });
        builder.Property(c => c.Floor).HasDefaultValue(0);
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne<FloorPlan>()
            .WithMany()
            .HasForeignKey(c => new { c.MuseumId, c.Floor })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
