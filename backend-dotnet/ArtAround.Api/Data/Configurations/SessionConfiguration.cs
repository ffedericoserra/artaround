using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.Property(s => s.Id).UseIdentityAlwaysColumn();

        // Partial index: a code can be reused once its session has ended.
        builder.HasIndex(s => s.Code).IsUnique().HasFilter("is_active");

        builder.Property(s => s.Version).IsRowVersion();
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.HasOne(s => s.Owner).WithMany().HasForeignKey(s => s.OwnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(s => s.Visit).WithMany().HasForeignKey(s => s.VisitId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Participants).WithOne().HasForeignKey(p => p.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Activities).WithOne().HasForeignKey(a => a.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Messages).WithOne().HasForeignKey(m => m.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Responses).WithOne().HasForeignKey(r => r.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}
