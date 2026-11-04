using System.ComponentModel.DataAnnotations;

namespace ArtAround.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; set; } = "";
    [Required] public string Audience { get; set; } = "";
    [Required, MinLength(32)] public string Secret { get; set; } = "";
    [Range(1, 1440)] public int ExpiryMinutes { get; set; }
}
