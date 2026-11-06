using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ArtAround.Api.Infrastructure;

public static partial class Slug
{
    public static string From(string text)
    {
        var withoutAccents = new StringBuilder();
        foreach (var c in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                withoutAccents.Append(c);
            }
        }

        var cleaned = SpecialChars().Replace(withoutAccents.ToString(), "").Trim();
        return Hyphens().Replace(Spaces().Replace(cleaned, "-"), "-");
    }

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex SpecialChars();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"-+")]
    private static partial Regex Hyphens();
}
