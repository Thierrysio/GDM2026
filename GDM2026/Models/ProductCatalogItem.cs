using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace GDM2026.Models;

public class ProductCatalogItem
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("nom")]
    public string? Nom { get; set; }

    [JsonProperty("name")]
    private string? EnglishName
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(Nom))
            {
                Nom = value;
            }
        }
    }

    [JsonProperty("titre")]
    private string? LegacyTitle
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(Nom))
            {
                Nom = value;
            }
        }
    }

    [JsonProperty("descriptioncourte")]
    public string? DescriptionCourte { get; set; }

    [JsonProperty("description")]
    public string? DescriptionLongue { get; set; }

    [JsonProperty("categorie")]
    public string? Categorie { get; set; }

    [JsonProperty("prix")]
    public double Prix { get; set; }

    [JsonProperty("prixPromo")]
    public double PrixPromo { get; set; }

    [JsonProperty("prix_promo")]
    private double PrixPromoSnake
    {
        set => PrixPromo = value;
    }

    [JsonProperty("image")]
    public string? ImageUrl { get; set; }

    [JsonProperty("images")]
    public List<string>? Images { get; set; }

    [JsonProperty("stock")]
    public int? Stock { get; set; }

    [JsonProperty("quantite")]
    private int? LegacyQuantity
    {
        set => Stock = value;
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Nom) ? $"Produit #{Id}" : Nom!;

    public string Description => !string.IsNullOrWhiteSpace(DescriptionCourte)
        ? DescriptionCourte!
        : (DescriptionLongue ?? string.Empty);

    public bool HasPromo => PrixPromo > 0 && PrixPromo < (Prix <= 0 ? PrixPromo : Prix);

    public string DisplayPrice
    {
        get
        {
            var culture = CultureInfo.GetCultureInfo("fr-FR");
            if (HasPromo)
            {
                return string.Format(culture, "{0:C} (au lieu de {1:C})", PrixPromo, Math.Max(Prix, PrixPromo));
            }

            var amount = Prix > 0 ? Prix : PrixPromo;
            return amount > 0
                ? string.Format(culture, "{0:C}", amount)
                : "Prix non renseigné";
        }
    }

    public string StockLabel => Stock.HasValue ? $"Stock : {Stock}" : "Stock indisponible";

    public string? PrimaryImage =>
        !string.IsNullOrWhiteSpace(ImageUrl)
            ? ImageUrl
            : Images?.Find(img => !string.IsNullOrWhiteSpace(img));

    public string? PrimaryImageFullUrl => BuildFullUrl(PrimaryImage);

    private static string? BuildFullUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (Uri.TryCreate(raw, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        var baseUrl = Constantes.BaseImagesAddress?.TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrEmpty(baseUrl))
        {
            return raw;
        }

        var relative = raw.TrimStart('/');
        return $"{baseUrl}/{relative}";
    }
}
