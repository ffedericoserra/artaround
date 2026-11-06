using ArtAround.Api.Domain;

namespace ArtAround.Api.Infrastructure;

/// <summary>A two-way map between an enum and its exact JSON spelling.</summary>
public class EnumLabels<T>(params (string Label, T Value)[] pairs) where T : struct, Enum
{
    private readonly Dictionary<string, T> _byLabel = pairs.ToDictionary(p => p.Label, p => p.Value);
    private readonly Dictionary<T, string> _byValue = pairs.ToDictionary(p => p.Value, p => p.Label);

    public bool IsValid(string? label) => label is not null && _byLabel.ContainsKey(label);
    public T Parse(string label) => _byLabel[label];
    public string Of(T value) => _byValue[value];
}

public static class Labels
{
    public static readonly EnumLabels<Tone> Tones = new(("easy", Tone.Easy), ("medium", Tone.Medium), ("complex", Tone.Complex));

    public static readonly EnumLabels<License> Licenses = new(
        ("CC-BY", License.CcBy), ("CC-BY-SA", License.CcBySa), ("CC-BY-NC", License.CcByNc),
        ("Copyright", License.Copyright), ("Public Domain", License.PublicDomain));

    public static readonly EnumLabels<VisitType> VisitTypes = new(("standard", VisitType.Standard), ("synchronized", VisitType.Synchronized));

    public static readonly EnumLabels<VisitLength> VisitLengths = new(("quick", VisitLength.Quick), ("normal", VisitLength.Normal), ("deep", VisitLength.Deep));

    public static readonly EnumLabels<BlockType> BlockTypes = new(("artwork", BlockType.Artwork), ("questions", BlockType.Questions));

    public static readonly EnumLabels<AnswerType> AnswerTypes = new(("open", AnswerType.Open), ("multiple-choice", AnswerType.MultipleChoice));

    public static readonly EnumLabels<Language> Languages = new(("it", Language.It), ("en", Language.En));
}
