using System.Text.Json.Serialization;
using ArtAround.Api.Domain;
using ArtAround.Api.Infrastructure;

namespace ArtAround.Api.Dtos;

public record UserRefDto([property: JsonPropertyName("_id")] string Id, string Username)
{
    public static UserRefDto From(User u) => new(u.Id.ToString(), u.Username);
}

public record ItemTextDto(string Tone, string Text);

public record ItemDto(
    [property: JsonPropertyName("_id")] string Id,
    string ContentId,
    UserRefDto CreatorId,
    string TargetAudience,
    List<ItemTextDto> Texts,
    int Price,
    string License,
    bool IsPublic,
    List<ContentDto> AssociatedContents,
    bool IsOwned,
    bool IsPurchased,
    int AdoptionPrice,
    int SalesCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Revenue,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    // Purchases arrive in Stage 7: until then nobody has bought anything.
    public static ItemDto From(Item i, long? viewerId)
    {
        var isOwned = i.CreatorId == viewerId;
        return new(
            i.Id.ToString(), i.Content.UniversalId, UserRefDto.From(i.Creator), i.TargetAudience,
            i.Texts.OrderBy(t => t.Tone).Select(t => new ItemTextDto(Labels.Tones.Of(t.Tone), t.Text)).ToList(),
            i.Price, Labels.Licenses.Of(i.License), i.IsPublic,
            i.AssociatedContents.Select(ContentDto.From).ToList(),
            isOwned, IsPurchased: false, AdoptionPrice: isOwned ? 0 : i.Price, SalesCount: 0, Revenue: isOwned ? 0 : null,
            i.CreatedAt, i.UpdatedAt);
    }
}

public record ItemTextRequest(string? Tone, string? Text);

public record CreateItemRequest(
    string? ContentId,
    string? TargetAudience,
    List<ItemTextRequest>? Texts,
    int? Price,
    string? License,
    bool? IsPublic,
    List<string>? AssociatedContents)
{
    public List<FieldError> Validate()
    {
        var errors = ItemRules.Validate(TargetAudience, Texts, Price, License);
        if (string.IsNullOrWhiteSpace(ContentId))
        {
            errors.Insert(0, new("contentId", "Content ID is required"));
        }
        if (string.IsNullOrWhiteSpace(TargetAudience))
        {
            errors.Add(new("targetAudience", "Target audience is required"));
        }
        if (Texts is null or { Count: 0 })
        {
            errors.Add(new("texts", "At least one text is required"));
        }

        return errors;
    }
}

public record UpdateItemRequest(
    string? TargetAudience,
    List<ItemTextRequest>? Texts,
    int? Price,
    string? License,
    bool? IsPublic,
    List<string>? AssociatedContents)
{
    public List<FieldError> Validate()
    {
        var errors = ItemRules.Validate(TargetAudience, Texts, Price, License);
        if (Texts is { Count: 0 })
        {
            errors.Add(new("texts", "At least one text is required"));
        }

        return errors;
    }
}

public static class ItemRules
{
    private static readonly string[] Audiences = ["general", "children", "student", "expert", "tourist"];

    /// <summary>Rules for the fields sent; the request decides which ones are required.</summary>
    public static List<FieldError> Validate(string? targetAudience, List<ItemTextRequest>? texts, int? price, string? license)
    {
        var errors = new List<FieldError>();

        if (!string.IsNullOrWhiteSpace(targetAudience) && !Audiences.Contains(targetAudience.Trim()))
        {
            errors.Add(new("targetAudience", "Invalid target audience"));
        }

        foreach (var text in texts ?? [])
        {
            if (!Labels.Tones.IsValid(text.Tone))
            {
                errors.Add(new("texts", "Invalid tone"));
            }
            if (string.IsNullOrWhiteSpace(text.Text))
            {
                errors.Add(new("texts", "Text is required"));
            }
        }
        if (texts is not null && texts.Select(t => t.Tone).Distinct().Count() != texts.Count)
        {
            errors.Add(new("texts", "Only one text per tone"));
        }

        if (price < 0)
        {
            errors.Add(new("price", "Price must not be negative"));
        }
        if (license is not null && !Labels.Licenses.IsValid(license))
        {
            errors.Add(new("license", "Invalid license"));
        }

        return errors;
    }
}
