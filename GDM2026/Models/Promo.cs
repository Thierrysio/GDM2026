using System;
using Newtonsoft.Json;
using PreserveAttribute = Microsoft.Maui.Controls.Internals.PreserveAttribute;

namespace GDM2026.Models;

[Preserve(AllMembers = true)]
public class Promo
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("dateDebut")]
    public DateTime? DateDebut { get; set; }

    [JsonProperty("dateFin")]
    public DateTime? DateFin { get; set; }

    [JsonProperty("prix")]
    public double Prix { get; set; }

    [JsonProperty("leProduitId")]
    public int? LeProduitId { get; set; }

    [JsonProperty("laCategoriePromoId")]
    public int? LaCategoriePromoId { get; set; }

    [JsonProperty("leProduit")]
    public PromoProduct? LeProduit { get; set; }

    [JsonProperty("laCategoriePromo")]
    public PromoCategory? LaCategoriePromo { get; set; }

    public string DisplayPeriod
    {
        get
        {
            if (DateDebut.HasValue && DateFin.HasValue)
                return $"Du {DateDebut.Value:g} au {DateFin.Value:g}";
            if (DateDebut.HasValue)
                return $"Début : {DateDebut.Value:g}";
            if (DateFin.HasValue)
                return $"Fin : {DateFin.Value:g}";
            return "Dates non renseignées";
        }
    }

    public string DisplayPrix => Prix > 0 ? $"{Prix:0.##} €" : "Prix non défini";

    public string DisplayProductName => LeProduit?.DisplayName ?? "Produit non défini";

    public string DisplayCategoryName => LaCategoriePromo?.DisplayName ?? "Catégorie non définie";
}
