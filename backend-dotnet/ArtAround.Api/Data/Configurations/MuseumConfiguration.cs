using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class MuseumConfiguration : IEntityTypeConfiguration<Museum>
{
    public void Configure(EntityTypeBuilder<Museum> builder)
    {
        builder.ToTable("museums", t =>
        {
            t.HasCheckConstraint("museums_slug_format", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
        });
        builder.Property(m => m.Id).UseIdentityAlwaysColumn();
        builder.HasIndex(m => m.Slug).IsUnique();
        builder.Property(m => m.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(m => m.UpdatedAt).HasDefaultValueSql("now()");
    }
}
