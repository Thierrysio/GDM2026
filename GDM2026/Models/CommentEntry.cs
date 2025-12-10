using Newtonsoft.Json;
using System;
using System.Globalization;

namespace GDM2026.Models;

public class CommentEntry
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("commentaire")]
    public string? Commentaire { get; set; }

    [JsonProperty("texte")]
    private string? Texte
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(Commentaire))
            {
                Commentaire = value;
            }
        }
    }

    [JsonProperty("comment")]
    private string? CommentEn
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(Commentaire))
            {
                Commentaire = value;
            }
        }
    }

    [JsonProperty("note")]
    public double? Note { get; set; }

    [JsonProperty("rating")]
    private double? Rating
    {
        set
        {
            if (!Note.HasValue)
            {
                Note = value;
            }
        }
    }

    [JsonProperty("date")]
    public DateTime? DateCommentaire { get; set; }

    [JsonProperty("created_at")]
    private DateTime? CreatedAt
    {
        set
        {
            if (!DateCommentaire.HasValue)
            {
                DateCommentaire = value;
            }
        }
    }

    [JsonProperty("utilisateur")]
    public string? UserName { get; set; }

    [JsonProperty("nom_utilisateur")]
    private string? NomUtilisateur
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(UserName))
            {
                UserName = value;
            }
        }
    }

    [JsonProperty("produit")]
    public string? ProductName { get; set; }

    [JsonProperty("nom_produit")]
    private string? NomProduit
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(ProductName))
            {
                ProductName = value;
            }
        }
    }

    public string DisplayUser => string.IsNullOrWhiteSpace(UserName) ? "Utilisateur inconnu" : UserName!;

    public string DisplayProduct => string.IsNullOrWhiteSpace(ProductName) ? "Produit non renseigné" : ProductName!;

    public string DisplayDate => DateCommentaire?.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("fr-FR"))
        ?? "Date inconnue";

    public string DisplayNote => Note.HasValue ? $"{Note:0.0}/5" : "Sans note";

    public string DisplayComment => string.IsNullOrWhiteSpace(Commentaire)
        ? "(Commentaire vide)"
        : Commentaire!;
}
