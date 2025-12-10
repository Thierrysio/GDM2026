using Newtonsoft.Json;
using System;
using System.Globalization;

namespace GDM2026.Models;

public class HistoryEntry
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("titre")]
    public string? Titre { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("texte")]
    public string? Texte
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(Description))
            {
                Description = value;
            }
        }
    }

    [JsonProperty("date")]
    public DateTime? DateHistoire { get; set; }

    [JsonProperty("date_histoire")]
    private DateTime? DateHistoireAlternative
    {
        set
        {
            if (!DateHistoire.HasValue)
            {
                DateHistoire = value;
            }
        }
    }

    [JsonProperty("image")]
    public string? Image { get; set; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Titre)
        ? $"Histoire #{Id}"
        : Titre!;

    public string DisplayDescription => string.IsNullOrWhiteSpace(Description)
        ? "(Description non renseignée)"
        : Description!;

    public string DisplayDate => DateHistoire?.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("fr-FR"))
        ?? "Date non renseignée";

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
