using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> builder)
    {
        builder.ToTable("visits");
        builder.Property(v => v.Id).UseIdentityAlwaysColumn();
        builder.HasIndex(v => v.Slug);
        builder.HasIndex(v => new { v.MuseumId, v.IsPublic });

        // ValueGeneratedNever: EF always sends these. Otherwise it treats false as "not set"
        // and the database default turns isPublic: false into true.
        builder.Property(v => v.Type).HasDefaultValue(VisitType.Standard).ValueGeneratedNever();
        builder.Property(v => v.Length).HasDefaultValue(VisitLength.Normal).ValueGeneratedNever();
        builder.Property(v => v.IsPublic).HasDefaultValue(true).ValueGeneratedNever();

        builder.Property(v => v.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(v => v.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.HasOne(v => v.Museum).WithMany().HasForeignKey(v => v.MuseumId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(v => v.Creator).WithMany().HasForeignKey(v => v.CreatorId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(v => v.Blocks).WithOne().HasForeignKey(b => b.VisitId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(v => v.Quiz).WithOne().HasForeignKey(q => q.VisitId).OnDelete(DeleteBehavior.Cascade);
    }
}
