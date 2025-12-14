using Newtonsoft.Json;
using System.Collections.Generic;

namespace GDM2026.Models;

public class ProductUpdateRequest
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("nom")]
    public string Nom { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("descriptioncourte")]
    public string DescriptionCourte { get; set; } = string.Empty;

    [JsonProperty("prix")]
    public double Prix { get; set; }

    [JsonProperty("quantite")]
    public int Quantite { get; set; }

    [JsonProperty("categorie")]
    public string Categorie { get; set; } = string.Empty;

    [JsonProperty("image")]
    public string? Image { get; set; }

    [JsonProperty("images")]
    public List<string> Images { get; set; } = new();
}
