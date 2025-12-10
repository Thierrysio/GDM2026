using System;
using Newtonsoft.Json;

namespace GDM2026.Models;

public class Actualite
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("titre")]
    public string Titre { get; set; } = string.Empty;

    [JsonProperty("texte")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("image")]
    public string? Image { get; set; }

    [JsonProperty("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public string FullImageUrl => BuildFullUrl(Image);

    private static string BuildFullUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var sanitized = path.Replace("\\", "/").Trim();

        if (Uri.TryCreate(sanitized, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        var trimmedPath = sanitized.TrimStart('/');
        var baseAddress = Constantes.BaseImagesAddress.TrimEnd('/');
        return $"{baseAddress}/{trimmedPath}";
    }
}
