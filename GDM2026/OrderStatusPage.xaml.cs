using GDM2026.Models;
using GDM2026.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace GDM2026;

[QueryProperty(nameof(Status), "status")]
public partial class OrderStatusPage : ContentPage
{
    private readonly Apis _apis = new();
    private readonly IReadOnlyList<string> _availableStatuses = new List<string>
    {
        "Confirmée",
        "Traitée",
        "Livrée",
        "En attente",
        "Annulée"
    };
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
                .SelectMany(order =>
                    (order.LesCommandes ?? new List<OrderLine>()).Select(line => new OrderStatusProduct
                    {
                        OrderId = order.Id,
                        OrderLineId = line.Id,
                        CurrentStatus = order.Etat ?? Status ?? "Inconnu",
                        Name = line.LeProduit?.NomProduit ?? "Produit inconnu",
                        ImageUrl = BuildImageUrl(line.LeProduit?.ImageUrl)
                    }))
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

    private async void OnChangeStatusRequested(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not OrderStatusProduct product)
        {
            return;
        }

        var options = _availableStatuses
            .Concat(string.IsNullOrWhiteSpace(product.CurrentStatus) ? Array.Empty<string>() : new[] { product.CurrentStatus })
            .Distinct()
            .ToArray();

        var selection = await DisplayActionSheet(
            "Modifier l'état de la commande",
            "Annuler",
            null,
            options);

        if (string.IsNullOrWhiteSpace(selection) || selection == product.CurrentStatus)
        {
            return;
        }

        await UpdateOrderStatusAsync(product, selection).ConfigureAwait(false);
    }

    private async Task UpdateOrderStatusAsync(OrderStatusProduct product, string newStatus)
    {
        var endpoint = "https://dantecmarket.com/api/mobile/modifierEtatCommande";
        var request = new UpdateOrderStatusRequest
        {
            CommandeId = product.OrderId,
            Etat = newStatus
        };

        try
        {
            var success = await _apis.PostBoolAsync(endpoint, request).ConfigureAwait(false);

            if (!success)
            {
                await ShowLoadErrorAsync("Impossible de modifier l'état de cette commande.");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                product.CurrentStatus = newStatus;
            });
        }
        catch (TaskCanceledException)
        {
            await ShowLoadErrorAsync("La mise à jour du statut a expiré. Veuillez réessayer.");
        }
        catch (HttpRequestException)
        {
            await ShowLoadErrorAsync("Impossible de mettre à jour le statut de cette commande.");
        }
    }
}

public class OrderStatusProduct : INotifyPropertyChanged
{
    private string _currentStatus = string.Empty;

    public int OrderId { get; set; }

    public int OrderLineId { get; set; }

    public string CurrentStatus
    {
        get => _currentStatus;
        set => SetProperty(ref _currentStatus, value);
    }

    public string Name { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T backingStore, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
        {
            return false;
        }

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
