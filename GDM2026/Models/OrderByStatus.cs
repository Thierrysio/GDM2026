using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Newtonsoft.Json;
using System.Linq;
using System.Runtime.CompilerServices;

namespace GDM2026.Models;

public class OrderByStatus
{
    public int Id { get; set; }

    public DateTime DateCommande { get; set; }

    public double MontantTotal { get; set; }

    public List<OrderLine> LesCommandes { get; set; } = new();

    public bool Valider { get; set; }

    public string? Etat { get; set; }

    [JsonProperty("userId")]
    public int? UserId { get; set; }

    [JsonProperty("clientId")]
    private int? ClientIdAlias { set => UserId = value; get => UserId; }

    [JsonProperty("idUser")]
    private int? IdUserAlias { set => UserId = value; get => UserId; }

    public string? NomClient { get; set; }

    public string? PrenomClient { get; set; }

    public string? PlanningDetails { get; set; }

    public string? Jour { get; set; }

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

public class OrderLine : INotifyPropertyChanged
{
    private bool _traite;
    private bool _livree;
    private int _quantite;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; set; }

    public int OrderId { get; set; }

    public int Quantite
    {
        get => _quantite;
        set => SetProperty(ref _quantite, value);
    }

    public ProductSummary? LeProduit { get; set; }

    [JsonProperty("prixRetenu")]
    public double PrixRetenu { get; set; }

    [JsonProperty("noteDonnee")]
    public bool NoteDonnee { get; set; }

    public bool Traite
    {
        get => _traite;
        set => SetProperty(ref _traite, value);
    }

    [JsonProperty("livre")]
    public bool Livree
    {
        get => _livree;
        set => SetProperty(ref _livree, value);
    }

    [JsonProperty("nomProduit")]
    public string? NomProduit
    {
        get => LeProduit?.NomProduit;
        set
        {
            if (LeProduit is null)
            {
                LeProduit = new ProductSummary();
            }

            LeProduit.NomProduit = value;
        }
    }

    [JsonProperty("produitId")]
    public int ProduitId
    {
        get => LeProduit?.Id ?? default;
        set
        {
            if (LeProduit is null)
            {
                LeProduit = new ProductSummary();
            }

            LeProduit.Id = value;
        }
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
        {
            return false;
        }

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class ProductSummary
{
    public int Id { get; set; }

    public string? NomProduit { get; set; }

    public string? Descriptioncourte { get; set; }

    public string? ImageUrl { get; set; }
}
