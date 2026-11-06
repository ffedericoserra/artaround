using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class SessionQuizAnswerConfiguration : IEntityTypeConfiguration<SessionQuizAnswer>
{
    public void Configure(EntityTypeBuilder<SessionQuizAnswer> builder)
    {
        builder.ToTable("session_quiz_answers");
        builder.HasKey(a => new { a.SessionId, a.UserId, a.QuestionIndex });
    }
}
