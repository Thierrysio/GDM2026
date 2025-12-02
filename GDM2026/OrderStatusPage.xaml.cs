using GDM2026.Models;
using GDM2026.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace GDM2026;

[QueryProperty(nameof(Status), "status")]
public partial class OrderStatusPage : ContentPage
{
    private readonly Apis _apis = new();
    private bool _hasLoaded;

    public OrderStatusPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public ObservableCollection<OrderStatusProduct> Products { get; } = new();

    public string? Status { get; set; }

    public bool IsLoading { get; private set; }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasLoaded)
        {
            _ = LoadStatusAsync();
        }
    }

    private async Task LoadStatusAsync()
    {
        if (string.IsNullOrWhiteSpace(Status))
        {
            return;
        }

        _hasLoaded = true;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            IsLoading = true;
            OnPropertyChanged(nameof(IsLoading));
            StatusTitle.Text = $"Commandes : {Status}";
            StatusSubtitle.Text = "Produits associés à cet état.";
        });

        try
        {
            var endpoint = "https://dantecmarket.com/api/mobile/commandesParEtat";
            var request = new OrderStatusRequest { Cd = Status };
            var orders = await _apis
                .PostAsync<OrderStatusRequest, List<OrderByStatus>>(endpoint, request)
                .ConfigureAwait(false);

            var items = (orders ?? new List<OrderByStatus>())
                .SelectMany(o => o.LesCommandes ?? new List<OrderLine>())
                .Select(line => new OrderStatusProduct
                {
                    Name = line.LeProduit?.NomProduit ?? "Produit inconnu",
                    ImageUrl = BuildImageUrl(line.LeProduit?.ImageUrl)
                })
                .ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Products.Clear();
                foreach (var item in items)
                {
                    Products.Add(item);
                }
            });
        }
        catch (TaskCanceledException)
        {
            await ShowLoadErrorAsync("Le chargement a expiré. Veuillez vérifier votre connexion.");
        }
        catch (HttpRequestException)
        {
            await ShowLoadErrorAsync("Impossible de récupérer les produits pour cet état.");
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsLoading));
            });
        }
    }

    private static string BuildImageUrl(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return "dotnet_bot.png";
        }

        if (Uri.TryCreate(imagePath, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (Uri.TryCreate(Constantes.BaseImagesAddress, UriKind.Absolute, out var baseUri))
        {
            return new Uri(baseUri, imagePath.TrimStart('/')).ToString();
        }

        return imagePath;
    }

    private async Task ShowLoadErrorAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DisplayAlert("Erreur", message, "OK");
        });
    }
}

public class OrderStatusProduct
{
    public string Name { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;
}
