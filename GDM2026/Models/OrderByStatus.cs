using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GDM2026.Models;

public class OrderByStatus
{
    public int Id { get; set; }

    public DateTime DateCommande { get; set; }

    public double MontantTotal { get; set; }

    public List<OrderLine> LesCommandes { get; set; } = new();

    public bool Valider { get; set; }

    public string? Etat { get; set; }

    public string? NomClient { get; set; }

    public string? PrenomClient { get; set; }

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

public class ProductSummary
{
    public int Id { get; set; }

    public string? NomProduit { get; set; }

    public string? Descriptioncourte { get; set; }

    public string? ImageUrl { get; set; }
}
