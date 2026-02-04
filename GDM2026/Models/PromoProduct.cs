using Newtonsoft.Json;
using PreserveAttribute = Microsoft.Maui.Controls.Internals.PreserveAttribute;

namespace GDM2026.Models;

[Preserve(AllMembers = true)]
public class PromoProduct
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("nomProduit")]
    public string? NomProduit { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("prix")]
    public double Prix { get; set; }

    [JsonProperty("descriptioncourte")]
    public string? DescriptionCourte { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(NomProduit)
        ? $"Produit #{Id}"
        : NomProduit!;

    public string DisplayDescription => !string.IsNullOrWhiteSpace(DescriptionCourte)
        ? DescriptionCourte!
        : (Description ?? string.Empty);

    public string DisplayPrice => Prix > 0 ? $"{Prix:0.##} €" : "Prix non défini";
}
