using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace GDM2026.Models;

public class OrderByStatus
{
    public int Id { get; set; }

    public DateTime DateCommande { get; set; }

    public double MontantTotal { get; set; }

    [JsonConverter(typeof(OrderLineListConverter))]
    public List<OrderLine> LesCommandes { get; set; } = new();

    public bool Valider { get; set; }

    public string? Etat { get; set; }

    public string? PlanningDetails { get; set; }

    public string DisplayTitle => $"Commande #{Id}";

    public string DisplayDate => DateCommande.ToString("dd/MM/yyyy HH:mm");

    public string DisplayAmount => MontantTotal.ToString("C", CultureInfo.GetCultureInfo("fr-FR"));

    public string ItemsSummary
    {
        get
        {
            if (LesCommandes is null || LesCommandes.Count == 0)
            {
                return "Aucun article";
            }

            var firstItems = LesCommandes
                .Where(item => item?.LeProduit?.NomProduit is not null)
                .Select(item => $"{item?.Quantite ?? 0} x {item?.LeProduit?.NomProduit}")
                .Take(2)
                .ToList();

            var summary = string.Join(", ", firstItems);
            var remaining = (LesCommandes?.Count ?? 0) - firstItems.Count;

            if (remaining > 0)
            {
                summary += $" (+{remaining} autre(s))";
            }

            return summary;
        }
    }
}

public class OrderLine
{
    public int Id { get; set; }

    public int Quantite { get; set; }

    public ProductSummary? LeProduit { get; set; }

    public double Prixretenu { get; set; }

    public bool NoteDonnee { get; set; }
}

public class OrderLineListConverter : JsonConverter<List<OrderLine>>
{
    public override List<OrderLine> ReadJson(JsonReader reader, Type objectType, List<OrderLine> existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        var items = new List<OrderLine>();

        if (reader.TokenType == JsonToken.Null)
        {
            return items;
        }

        var array = serializer.Deserialize<List<object>>(reader);
        if (array == null)
        {
            return items;
        }

        foreach (var element in array)
        {
            switch (element)
            {
                case null:
                    continue;
                case long idValue:
                    items.Add(new OrderLine { Id = (int)idValue });
                    break;
                case int idInt:
                    items.Add(new OrderLine { Id = idInt });
                    break;
                default:
                    var token = element as Newtonsoft.Json.Linq.JToken;
                    if (token != null)
                    {
                        var line = token.ToObject<OrderLine>(serializer);
                        if (line != null)
                        {
                            items.Add(line);
                        }
                    }
                    break;
            }
        }

        return items;
    }

    public override void WriteJson(JsonWriter writer, List<OrderLine> value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }
}

public class ProductSummary
{
    public int Id { get; set; }

    public string? NomProduit { get; set; }

    public string? Descriptioncourte { get; set; }

    public string? ImageUrl { get; set; }
}
