using System;
using Newtonsoft.Json;

namespace GDM2026.Models;

public class Partner
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("nom")]
    public string? Nom { get; set; }

    [JsonProperty("url")]
    public string? Url { get; set; }

    [JsonProperty("site")]
    public string? Site { get; set; }

    [JsonProperty("site_web")]
    public string? SiteWeb { get; set; }

    [JsonProperty("lien")]
    public string? Lien { get; set; }

    [JsonProperty("image")]
    public string? Image { get; set; }

    [JsonProperty("logo")]
    public string? Logo { get; set; }

    [JsonProperty("photo")]
    public string? Photo { get; set; }

    public string DisplayName => !string.IsNullOrWhiteSpace(Name)
        ? Name!
        : Nom ?? "Partenaire";

    public string? Website => FirstNonEmpty(Url, Site, SiteWeb, Lien);

    public string WebsiteDisplay => string.IsNullOrWhiteSpace(Website)
        ? "Site non renseigné"
        : Website!;

    public string? ImagePath => FirstNonEmpty(Image, Logo, Photo);

    public string FullImageUrl => BuildFullUrl(ImagePath);

    public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath);

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

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
