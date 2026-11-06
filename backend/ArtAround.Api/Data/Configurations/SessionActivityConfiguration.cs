using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class SessionActivityConfiguration : IEntityTypeConfiguration<SessionActivity>
{
    public void Configure(EntityTypeBuilder<SessionActivity> builder)
    {
        builder.ToTable("session_activities");
        builder.Property(a => a.Id).UseIdentityAlwaysColumn();
        builder.Property(a => a.Action).HasMaxLength(20);
        builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
