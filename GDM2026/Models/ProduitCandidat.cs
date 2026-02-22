using Newtonsoft.Json;

namespace GDM2026.Models;

public class ProduitCandidat
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("nomProduit")]
    public string? NomProduit { get; set; }

    [JsonProperty("nom")]
    public string? Nom { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("descriptionCourte")]
    public string? DescriptionCourte { get; set; }

    [JsonProperty("prixEstime")]
    public decimal? PrixEstime { get; set; }

    [JsonProperty("prixPropose")]
    public decimal? PrixPropose { get; set; }

    [JsonProperty("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonProperty("categorie")]
    public string? Categorie { get; set; }

    [JsonProperty("categorieNom")]
    public string? CategorieNom { get; set; }

    [JsonProperty("sessionVoteId")]
    public int? SessionVoteId { get; set; }

    [JsonProperty("moyenneNotes")]
    public double? MoyenneNotes { get; set; }

    [JsonProperty("noteMoyenne")]
    public double? NoteMoyenne { get; set; }

    [JsonProperty("nombreVotes")]
    public int NombreVotes { get; set; }

    [JsonProperty("noteUtilisateur")]
    public double? NoteUtilisateur { get; set; }

    public string DisplayName => NomProduit ?? Nom ?? $"Produit #{Id}";

    public string DisplayCategorie => Categorie ?? CategorieNom ?? "Sans categorie";

    public string DisplayPrix
    {
        get
        {
            var prix = PrixEstime ?? PrixPropose;
            return prix.HasValue ? $"{prix:F2} EUR" : "Prix non defini";
        }
    }

    public double DisplayMoyenne => MoyenneNotes ?? NoteMoyenne ?? 0;

    public string MoyenneLabel => $"{DisplayMoyenne:F1}/5";

    public string VotesLabel => NombreVotes == 1 ? "1 vote" : $"{NombreVotes} votes";

    public bool HasUserVote => NoteUtilisateur.HasValue;

    public string UserVoteLabel => NoteUtilisateur.HasValue
        ? $"Votre note : {NoteUtilisateur:F0}/5"
        : "Pas encore vote";
}
