using Newtonsoft.Json;

namespace GDM2026.Models;

public class CreateProduitCandidatRequest
{
    [JsonProperty("sessionVoteId")]
    public int SessionVoteId { get; set; }

    [JsonProperty("nomProduit")]
    public string? NomProduit { get; set; }

    [JsonProperty("descriptionCourte")]
    public string? DescriptionCourte { get; set; }

    [JsonProperty("prixEstime")]
    public decimal? PrixEstime { get; set; }

    [JsonProperty("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonProperty("categorie")]
    public string? Categorie { get; set; }
}
