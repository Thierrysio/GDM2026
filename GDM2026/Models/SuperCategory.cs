using System.Collections.Generic;
using Newtonsoft.Json;

namespace GDM2026.Models;

public class SuperCategory
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("nom")]
    public string? Name { get; set; }

    [JsonProperty("lescategories")]
    public List<int> CategoryIds { get; set; } = new();

    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? $"Catégorie parente #{Id}"
        : Name!;
}
