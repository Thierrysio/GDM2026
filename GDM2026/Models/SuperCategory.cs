using System.Collections.Generic;
using Newtonsoft.Json;

namespace GDM2026.Models;

public class SuperCategory
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("nom")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("image")]
    public string? ImageUrl { get; set; }

    [JsonProperty("produits")]
    public List<int> Products { get; set; } = new();

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Super catégorie #{Id}" : Name!;

    public int ProductCount => Products?.Count ?? 0;
}
