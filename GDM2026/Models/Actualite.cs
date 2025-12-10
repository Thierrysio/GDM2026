using System;
using System.Collections.Generic;
using System.Linq;
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

    [JsonProperty("images")]
    public List<string> Images { get; set; } = new();

    [JsonProperty("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore]
    public string? PrimaryImage => !string.IsNullOrWhiteSpace(Image)
        ? Image
        : Images?.FirstOrDefault();

    public string FullImageUrl => BuildFullUrl(PrimaryImage);

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
