using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class PointOfInterestConfiguration : IEntityTypeConfiguration<PointOfInterest>
{
    public void Configure(EntityTypeBuilder<PointOfInterest> builder)
    {
        builder.ToTable("points_of_interest", t =>
        {
            t.HasCheckConstraint("points_of_interest_lat_range", "lat BETWEEN -90 AND 90");
            t.HasCheckConstraint("points_of_interest_lng_range", "lng BETWEEN -180 AND 180");
        });
        builder.Property(p => p.Id).UseIdentityAlwaysColumn();
        builder.Property(p => p.Floor).HasDefaultValue(0);
        builder.HasOne<FloorPlan>()
            .WithMany()
            .HasForeignKey(p => new { p.MuseumId, p.Floor })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
