using Newtonsoft.Json;

namespace GDM2026.Models;

public class PromoCategory
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("nom")]
    public string? Name { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? $"Catégorie promo #{Id}"
        : Name!;
}
