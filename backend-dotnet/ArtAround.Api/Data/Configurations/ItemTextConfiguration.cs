using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtAround.Api.Data.Configurations;

public class ItemTextConfiguration : IEntityTypeConfiguration<ItemText>
{
    public void Configure(EntityTypeBuilder<ItemText> builder)
    {
        builder.ToTable("item_texts");
        builder.HasKey(t => new { t.ItemId, t.Tone });
    }
}
