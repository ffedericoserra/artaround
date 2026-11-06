using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class SessionParticipantConfiguration : IEntityTypeConfiguration<SessionParticipant>
{
    public void Configure(EntityTypeBuilder<SessionParticipant> builder)
    {
        builder.ToTable("session_participants");
        builder.HasKey(p => new { p.SessionId, p.UserId });
        builder.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(p => p.QuizAnswers).WithOne().HasForeignKey(a => new { a.SessionId, a.UserId }).OnDelete(DeleteBehavior.Cascade);
    }
}
