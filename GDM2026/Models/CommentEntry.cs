using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Linq;

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

    [JsonProperty("dateCommentaire")]
    private DateTime? DateCommentaireDetail
    {
        set
        {
            if (!DateCommentaire.HasValue)
            {
                DateCommentaire = value;
            }
        }
    }

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

    [JsonProperty("leUser")]
    private CommentUser? LeUser
    {
        set
        {
            if (value is null)
            {
                return;
            }

            var fullName = string.Join(" ", new[] { value.Prenom, value.Nom }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            if (!string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(UserName))
            {
                UserName = fullName.Trim();
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

    [JsonProperty("leProduit")]
    private CommentProduct? LeProduit
    {
        set
        {
            if (value?.NomProduit != null && string.IsNullOrWhiteSpace(ProductName))
            {
                ProductName = value.NomProduit;
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

    private sealed class CommentUser
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("nom")]
        public string? Nom { get; set; }

        [JsonProperty("prenom")]
        public string? Prenom { get; set; }
    }

    private sealed class CommentProduct
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("nomProduit")]
        public string? NomProduit { get; set; }
    }
}
