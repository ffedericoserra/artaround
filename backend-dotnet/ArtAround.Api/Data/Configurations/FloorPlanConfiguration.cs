using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class FloorPlanConfiguration : IEntityTypeConfiguration<FloorPlan>
{
    public void Configure(EntityTypeBuilder<FloorPlan> builder)
    {
        builder.ToTable("floor_plans", t =>
        {
            t.HasCheckConstraint("floor_plans_lat_order", "south < north");
            t.HasCheckConstraint("floor_plans_lng_order", "west < east");
            t.HasCheckConstraint("floor_plans_lat_range", "south >= -90 AND north <= 90");
            t.HasCheckConstraint("floor_plans_lng_range", "west >= -180 AND east <= 180");
        });
        builder.HasKey(f => new { f.MuseumId, f.Floor });
    }
}
