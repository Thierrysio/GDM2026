using GDM2026.Models;
using GDM2026.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
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

    public static IReadOnlyList<string> StatusOptions { get; } = new[]
    {
        "Confirmée",
        "Traitée",
        "Livrée",
        "Annulée"
    };

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
                .SelectMany(o => o.LesCommandes ?? new List<OrderLine>(), (order, line) => new { order, line })
                .Select(x => new OrderStatusProduct
                {
                    OrderId = x.order.Id,
                    CurrentStatus = string.IsNullOrWhiteSpace(x.order.Etat) ? Status ?? "" : x.order.Etat!,
                    Name = x.line.LeProduit?.NomProduit ?? "Produit inconnu",
                    ImageUrl = BuildImageUrl(x.line.LeProduit?.ImageUrl)
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

    private async void OnStatusChanged(object sender, EventArgs e)
    {
        if (sender is not Picker picker ||
            picker.BindingContext is not OrderStatusProduct product ||
            picker.SelectedItem is not string newStatus ||
            string.Equals(product.CurrentStatus, newStatus, StringComparison.Ordinal))
        {
            return;
        }

        var previousStatus = product.CurrentStatus;
        var updated = await ChangeStatusAsync(product, newStatus).ConfigureAwait(false);

        if (!updated)
        {
            await MainThread.InvokeOnMainThreadAsync(() => picker.SelectedItem = previousStatus);
        }
    }

    private async Task<bool> ChangeStatusAsync(OrderStatusProduct product, string newStatus, CancellationToken ct = default)
    {
        if (product == null || product.OrderId <= 0 || string.IsNullOrWhiteSpace(newStatus))
        {
            return false;
        }

        try
        {
            var endpoint = "https://dantecmarket.com/api/mobile/updateEtatCommande";
            var request = new UpdateOrderStatusRequest
            {
                Id = product.OrderId,
                Etat = newStatus
            };

            var success = await _apis.PostBoolAsync(endpoint, request, ct).ConfigureAwait(false);

            if (!success)
            {
                await ShowLoadErrorAsync("La mise à jour de l'état a échoué.");
                return false;
            }

            await MainThread.InvokeOnMainThreadAsync(() => product.CurrentStatus = newStatus);
            return true;
        }
        catch (TaskCanceledException)
        {
            await ShowLoadErrorAsync("La mise à jour de l'état a expiré. Veuillez réessayer.");
        }
        catch (HttpRequestException)
        {
            await ShowLoadErrorAsync("Impossible de modifier l'état de cette commande.");
        }

        return false;
    }
}

public class OrderStatusProduct : INotifyPropertyChanged
{
    private string _currentStatus = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int OrderId { get; set; }

    public string CurrentStatus
    {
        get => _currentStatus;
        set => SetProperty(ref _currentStatus, value);
    }

    public string Name { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

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
