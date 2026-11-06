using System.Text.Json.Serialization;
using ArtAround.Api.Domain;
using ArtAround.Api.Infrastructure;

namespace ArtAround.Api.Dtos;

public record MuseumRefDto([property: JsonPropertyName("_id")] string Id, string Name, string Slug)
{
    public static MuseumRefDto From(Museum m) => new(m.Id.ToString(), m.Name, m.Slug);
}

public record StopItemRefDto([property: JsonPropertyName("_id")] string Id, string ContentId);

/// <summary>One stop of the derived sequence. TItem is how the item is embedded.</summary>
public record StopDto<TItem>(TItem ItemId, int Order, string NextDirections, string PrevDirections, string? OverrideImage);

public record BlockQuestionDto(
    [property: JsonPropertyName("_id")] string Id,
    string Prompt,
    string AnswerType,
    List<string> Options,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CorrectIndex);

public record BlockDto(
    [property: JsonPropertyName("_id")] string Id,
    string Type,
    string BlockName,
    List<string> Items,
    List<BlockQuestionDto> Questions);

public record QuizQuestionDto(
    string Question,
    List<string> Options,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CorrectIndex);

public record VisitSummaryDto(
    [property: JsonPropertyName("_id")] string Id,
    string Title,
    string Slug,
    string? Description,
    string? ImageUrl,
    MuseumRefDto MuseumId,
    UserRefDto CreatorId,
    List<StopDto<StopItemRefDto>> Sequence,
    string Type,
    string Length,
    bool IsPublic,
    int ItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static VisitSummaryDto From(Visit v)
    {
        var sequence = VisitMapping.Stops(v, i => new StopItemRefDto(i.Id.ToString(), i.Content.UniversalId));
        return new(
            v.Id.ToString(), v.Title, v.Slug, v.Description, v.ImageUrl, MuseumRefDto.From(v.Museum), UserRefDto.From(v.Creator),
            sequence, Labels.VisitTypes.Of(v.Type), Labels.VisitLengths.Of(v.Length), v.IsPublic, sequence.Count,
            v.CreatedAt, v.UpdatedAt);
    }
}

public record VisitDetailDto(
    [property: JsonPropertyName("_id")] string Id,
    string Title,
    string Slug,
    string? Description,
    string? ImageUrl,
    MuseumDto MuseumId,
    UserRefDto CreatorId,
    List<StopDto<ItemDto>> Sequence,
    List<BlockDto> Blocks,
    List<QuizQuestionDto> Quiz,
    string Type,
    string Length,
    bool IsPublic,
    int ItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>The quiz and question keys are for the visit's creator only.</summary>
    public static VisitDetailDto From(Visit v, long? viewerId)
    {
        var showAnswers = v.CreatorId == viewerId;
        var sequence = VisitMapping.Stops(v, i => ItemDto.From(i, viewerId));
        return new(
            v.Id.ToString(), v.Title, v.Slug, v.Description, v.ImageUrl, MuseumDto.From(v.Museum), UserRefDto.From(v.Creator),
            sequence,
            v.Blocks.OrderBy(b => b.Position).Select(b => new BlockDto(
                b.Id.ToString(), Labels.BlockTypes.Of(b.Type), b.Name,
                b.Items.OrderBy(i => i.Position).Select(i => i.ItemId.ToString()).ToList(),
                b.Questions.OrderBy(q => q.Position).Select(q => new BlockQuestionDto(
                    q.Id.ToString(), q.Prompt, Labels.AnswerTypes.Of(q.AnswerType), q.Options, showAnswers ? q.CorrectIndex : null)).ToList())).ToList(),
            v.Quiz.OrderBy(q => q.Position).Select(q => new QuizQuestionDto(q.Question, q.Options, showAnswers ? q.CorrectIndex : null)).ToList(),
            Labels.VisitTypes.Of(v.Type), Labels.VisitLengths.Of(v.Length), v.IsPublic, sequence.Count,
            v.CreatedAt, v.UpdatedAt);
    }
}

public record VisitWriteResponse(VisitDetailDto Visit, int ChargedAmount, int WalletBalance, List<string> AdoptedItemIds);

public static class VisitMapping
{
    /// <summary>The flat sequence is derived: artwork blocks' items, in order.</summary>
    public static List<StopDto<TItem>> Stops<TItem>(Visit v, Func<Item, TItem> mapItem) =>
        v.Blocks.OrderBy(b => b.Position)
            .SelectMany(b => b.Items.OrderBy(i => i.Position))
            .Select((stop, order) => new StopDto<TItem>(mapItem(stop.Item), order, stop.NextDirections, stop.PrevDirections, stop.OverrideImage))
            .ToList();
}

public record StopRequest(string? ItemId, string? NextDirections, string? PrevDirections, string? OverrideImage);

public record BlockQuestionRequest(string? Prompt, string? AnswerType, List<string>? Options, int? CorrectIndex);

public record BlockRequest(string? Type, string? BlockName, List<string>? Items, List<BlockQuestionRequest>? Questions);

public record QuizQuestionRequest(string? Question, List<string>? Options, int? CorrectIndex);

public record SaveVisitRequest(
    string? Title,
    string? MuseumId,
    string? Description,
    string? ImageUrl,
    List<StopRequest>? Sequence,
    List<BlockRequest>? Blocks,
    string? Type,
    string? Length,
    bool? IsPublic,
    List<QuizQuestionRequest>? Quiz)
{
    public List<FieldError> ValidateCreate()
    {
        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(Title))
        {
            errors.Add(new("title", "Visit title is required"));
        }
        if (!long.TryParse(MuseumId, out _))
        {
            errors.Add(new("museumId", "Museum ID is required"));
        }
        errors.AddRange(ValidateCommon());
        return errors;
    }

    /// <summary>Rules for the fields sent: an update changes only those.</summary>
    public List<FieldError> ValidateCommon()
    {
        var errors = new List<FieldError>();

        if (ImageUrl is not null && !IsUrl(ImageUrl))
        {
            errors.Add(new("imageUrl", "imageUrl must be a URL"));
        }
        if (Type is not null && !Labels.VisitTypes.IsValid(Type))
        {
            errors.Add(new("type", "Invalid visit type"));
        }
        if (Length is not null && !Labels.VisitLengths.IsValid(Length))
        {
            errors.Add(new("length", "Invalid visit length"));
        }

        foreach (var stop in Sequence ?? [])
        {
            if (!long.TryParse(stop.ItemId, out _))
            {
                errors.Add(new("sequence", "Item ID is required"));
            }
            if (stop.OverrideImage is not null && !IsUrl(stop.OverrideImage))
            {
                errors.Add(new("sequence", "overrideImage must be a URL"));
            }
        }

        foreach (var block in Blocks ?? [])
        {
            errors.AddRange(ValidateBlock(block));
        }

        foreach (var question in Quiz ?? [])
        {
            if (string.IsNullOrWhiteSpace(question.Question))
            {
                errors.Add(new("quiz", "Question is required"));
            }
            if (question.Options is not { Count: >= 2 })
            {
                errors.Add(new("quiz", "At least 2 options are required"));
            }
            if (question.CorrectIndex is not >= 0 || question.CorrectIndex >= question.Options?.Count)
            {
                errors.Add(new("quiz", "Select a valid correct option"));
            }
        }

        return errors;
    }

    private static List<FieldError> ValidateBlock(BlockRequest block)
    {
        var errors = new List<FieldError>();

        if (block.Type is not null && !Labels.BlockTypes.IsValid(block.Type))
        {
            errors.Add(new("blocks", "Invalid block type"));
        }
        if (block.Items?.Any(id => !long.TryParse(id, out _)) == true)
        {
            errors.Add(new("blocks", "Item ID is required"));
        }
        if (block.Type != "questions")
        {
            return errors;
        }

        if (block.Questions is null or { Count: 0 })
        {
            errors.Add(new("blocks", "Question sections require at least one question"));
        }
        foreach (var question in block.Questions ?? [])
        {
            if (string.IsNullOrWhiteSpace(question.Prompt))
            {
                errors.Add(new("blocks", "Question text is required"));
            }
            if (!Labels.AnswerTypes.IsValid(question.AnswerType))
            {
                errors.Add(new("blocks", "Invalid answer type"));
            }
            if (question.AnswerType == "multiple-choice"
                && (question.Options is not { Count: >= 2 } || question.CorrectIndex is not >= 0 || question.CorrectIndex >= question.Options.Count))
            {
                errors.Add(new("blocks", "Multiple-choice questions require 2 options and a valid correct option"));
            }
        }

        return errors;
    }

    private static bool IsUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out _);
}
