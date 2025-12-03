using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class OrderStatusPageViewModel : BaseViewModel
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

    private readonly Dictionary<OrderStatusProduct, string> _lastKnownStatuses = new();
    private readonly HashSet<OrderStatusProduct> _statusUpdatesInProgress = new();
    private bool _hasLoaded;
    private string? _status;
    private string _pageTitle = string.Empty;
    private string _subtitle = string.Empty;

    public OrderStatusPageViewModel()
    {
        Products = new ObservableCollection<OrderStatusProduct>();
        RevertStatusCommand = new Command<OrderStatusProduct>(async product => await RevertStatusAsync(product));
    }

    public ObservableCollection<OrderStatusProduct> Products { get; }

    public IReadOnlyList<string> AvailableStatuses => _availableStatuses;

    public ICommand RevertStatusCommand { get; }

    public string? Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string PageTitle
    {
        get => _pageTitle;
        set => SetProperty(ref _pageTitle, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        set => SetProperty(ref _subtitle, value);
    }

    public async Task InitializeAsync()
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await LoadStatusAsync().ConfigureAwait(false);
    }

    private async Task LoadStatusAsync()
    {
        if (string.IsNullOrWhiteSpace(Status))
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            IsBusy = true;
            PageTitle = $"Commandes : {Status}";
            Subtitle = "Produits : en cours de chargement...";
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
                _lastKnownStatuses.Clear();

                foreach (var item in items)
                {
                    Products.Add(item);
                    _lastKnownStatuses[item] = item.CurrentStatus;
                    AttachStatusHandler(item);
                }

                Subtitle = $"Produits : {Products.Count}";
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
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    private void AttachStatusHandler(OrderStatusProduct product)
    {
        product.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(OrderStatusProduct.CurrentStatus))
            {
                _ = OnProductStatusChangedAsync(product);
            }
        };
    }

    private async Task OnProductStatusChangedAsync(OrderStatusProduct product)
    {
        if (_statusUpdatesInProgress.Contains(product))
        {
            return;
        }

        var newStatus = product.CurrentStatus;
        var previousStatus = _lastKnownStatuses.TryGetValue(product, out var last) ? last : string.Empty;

        if (string.IsNullOrWhiteSpace(newStatus) || string.Equals(newStatus, previousStatus, StringComparison.Ordinal))
        {
            return;
        }

        await UpdateOrderStatusAsync(product, newStatus, isReverting: false);
    }

    private async Task RevertStatusAsync(OrderStatusProduct? product)
    {
        if (product?.PreviousStatus == null)
        {
            return;
        }

        await UpdateOrderStatusAsync(product, product.PreviousStatus, isReverting: true);
    }

    private async Task<bool> UpdateOrderStatusAsync(OrderStatusProduct product, string newStatus, bool isReverting)
    {
        if (string.IsNullOrWhiteSpace(newStatus))
        {
            return false;
        }

        var previousStatus = _lastKnownStatuses.TryGetValue(product, out var last) ? last : product.CurrentStatus;

        if (string.Equals(previousStatus, newStatus, StringComparison.Ordinal))
        {
            if (isReverting)
            {
                product.ClearPreviousStatus();
            }

            return false;
        }

        var endpoint = "https://dantecmarket.com/api/mobile/updateEtat";
        var request = new UpdateOrderStatusRequest
        {
            Id = product.OrderId,
            Etat = newStatus
        };

        _statusUpdatesInProgress.Add(product);

        try
        {
            var success = await _apis.PostBoolAsync(endpoint, request).ConfigureAwait(false);

            if (!success)
            {
                await ShowLoadErrorAsync("Impossible de modifier l'état de cette commande.");
                await ResetProductStatusAsync(product, previousStatus);
                return false;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (isReverting)
                {
                    product.ClearPreviousStatus();
                }
                else
                {
                    product.RememberPreviousStatus(previousStatus);
                }

                SetProductStatusSilently(product, newStatus);
                _lastKnownStatuses[product] = newStatus;
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
        finally
        {
            _statusUpdatesInProgress.Remove(product);
        }

        await ResetProductStatusAsync(product, previousStatus);
        return false;
    }

    private async Task ResetProductStatusAsync(OrderStatusProduct product, string status)
    {
        await MainThread.InvokeOnMainThreadAsync(() => SetProductStatusSilently(product, status));
    }

    private void SetProductStatusSilently(OrderStatusProduct product, string status)
    {
        _statusUpdatesInProgress.Add(product);
        product.CurrentStatus = status;
        _statusUpdatesInProgress.Remove(product);
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

    private static async Task ShowLoadErrorAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Application.Current?.MainPage?.DisplayAlert("Erreur", message, "OK")!;
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
}
