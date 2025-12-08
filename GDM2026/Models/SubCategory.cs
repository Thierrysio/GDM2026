using Newtonsoft.Json;

namespace GDM2026.Models;

public class SubCategory
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("nom")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Sous-catégorie #{Id}" : Name!;
}
