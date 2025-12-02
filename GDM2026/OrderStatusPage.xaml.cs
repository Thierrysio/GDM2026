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

    public IReadOnlyList<string> AvailableStatuses => _availableStatuses;

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
            StatusSubtitle.Text = "Produits : en cours de chargement...";
        });

        try
        {
            var endpoint = "https://dantecmarket.com/api/mobile/commandesParEtat";
            var request = new OrderStatusRequest { Cd = Status };
            var orders = await _apis
                .PostAsync<OrderStatusRequest, List<OrderByStatus>>(endpoint, request)
                .ConfigureAwait(false);

            var items = (orders ?? new List<OrderByStatus>())
                .SelectMany(order => (order.LesCommandes ?? new List<OrderLine>())
                    .Select(line => new { order, line }))
                .Select(x => new OrderStatusProduct
                {
                    OrderId = x.order.Id,
                    OrderLineId = x.line.Id,
                    CustomerName = BuildCustomerName(x.order),
                    CurrentStatus = x.order.Etat ?? Status ?? "Inconnu",
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

                StatusSubtitle.Text = $"Produits : {Products.Count}";
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

    private static string BuildCustomerName(OrderByStatus order)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(order.PrenomClient))
        {
            parts.Add(order.PrenomClient);
        }

        if (!string.IsNullOrWhiteSpace(order.NomClient))
        {
            parts.Add(order.NomClient);
        }

        if (parts.Count > 0)
        {
            return string.Join(" ", parts);
        }

        return "Client inconnu";
    }

    private async void OnStatusSelectionChanged(object sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (sender is not Picker picker || picker.SelectedItem is not string selectedStatus)
            {
                return;
            }

            if (picker.BindingContext is not OrderStatusProduct product)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedStatus) || selectedStatus == product.CurrentStatus)
            {
                return;
            }

            var previousStatus = product.CurrentStatus;

            try
            {
                var updated = await UpdateOrderStatusAsync(product, selectedStatus, isReverting: false);

                if (!updated)
                {
                    picker.SelectedItem = previousStatus;
                }
            }
            catch (Exception ex)
            {
                picker.SelectedItem = previousStatus;
                await ShowLoadErrorAsync("Une erreur est survenue lors de la mise à jour du statut.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        });
    }

    private async void OnRevertStatusRequested(object sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not OrderStatusProduct product)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(product.PreviousStatus))
        {
            return;
        }

        await UpdateOrderStatusAsync(product, product.PreviousStatus, isReverting: true);
    }

    private async Task<bool> UpdateOrderStatusAsync(OrderStatusProduct product, string newStatus, bool isReverting)
    {
        var endpoint = "https://dantecmarket.com/api/mobile/updateEtat";
        var request = new UpdateOrderStatusRequest
        {
            Id = product.OrderId,
            Etat = newStatus
        };

        var previousStatus = product.CurrentStatus;

        if (string.Equals(previousStatus, newStatus, StringComparison.Ordinal))
        {
            if (isReverting)
            {
                product.ClearPreviousStatus();
            }

            return false;
        }

        try
        {
            var success = await _apis.PostBoolAsync(endpoint, request).ConfigureAwait(false);

            if (!success)
            {
                await ShowLoadErrorAsync("Impossible de modifier l'état de cette commande.");
                return false;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (isReverting)
                {
                    product.CurrentStatus = newStatus;
                    product.ClearPreviousStatus();
                }
                else
                {
                    product.RememberPreviousStatus(previousStatus);
                    product.CurrentStatus = newStatus;
                }
            });

            OrderStatusDeltaTracker.RecordChange(previousStatus, newStatus);

            return true;
        }
        catch (TaskCanceledException)
        {
            await ShowLoadErrorAsync("La mise à jour du statut a expiré. Veuillez réessayer.");
        }
        catch (HttpRequestException)
        {
            await ShowLoadErrorAsync("Impossible de mettre à jour le statut de cette commande.");
        }
        catch (Exception)
        {
            await ShowLoadErrorAsync("Une erreur inattendue empêche la mise à jour du statut.");
        }

        return false;
    }
}

public class OrderStatusProduct : INotifyPropertyChanged
{
    private string _currentStatus = string.Empty;
    private string _customerName = string.Empty;
    private string? _previousStatus;

    public int OrderId { get; set; }

    public int OrderLineId { get; set; }

    public string CurrentStatus
    {
        get => _currentStatus;
        set => SetProperty(ref _currentStatus, value);
    }

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public string? PreviousStatus
    {
        get => _previousStatus;
        private set
        {
            if (SetProperty(ref _previousStatus, value))
            {
                OnPropertyChanged(nameof(CanRevert));
            }
        }
    }

    public bool CanRevert => !string.IsNullOrWhiteSpace(PreviousStatus);

    public void RememberPreviousStatus(string status) => PreviousStatus = status;

    public void ClearPreviousStatus() => PreviousStatus = null;

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
