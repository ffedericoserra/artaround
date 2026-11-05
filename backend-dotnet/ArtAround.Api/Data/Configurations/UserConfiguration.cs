using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t =>
        {
            t.HasCheckConstraint("users_username_trimmed", "username = btrim(username)");
            t.HasCheckConstraint("users_username_min_len", "char_length(username) >= 3");
            t.HasCheckConstraint("users_wallet_nonnegative", "wallet_balance >= 0");
        });
        builder.Property(u => u.Id).UseIdentityAlwaysColumn();
        builder.Property(u => u.Username).HasMaxLength(30);
        builder.Property(u => u.Language).HasDefaultValue(Language.It);
        builder.Property(u => u.WalletBalance).HasDefaultValue(100);
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");
    }
}
