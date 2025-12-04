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

public partial class OrderStatusPageViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly IReadOnlyList<string> _availableStatuses =
    [
        "Confirmée",
        "En cours de traitement",
        "Traitée",
        "Livrée",
        "A confirmer"
    ];

    private readonly Dictionary<OrderStatusEntry, string> _lastKnownStatuses = [];
    private readonly HashSet<OrderStatusEntry> _statusUpdatesInProgress = [];
    private readonly List<OrderStatusEntry> _allOrders = [];
    private bool _hasLoaded;
    private string? _status;
    private string _pageTitle = string.Empty;
    private string _subtitle = string.Empty;
    private string _searchQuery = string.Empty;
    private bool _isShowingLimitedOrders = true;
    private bool _canShowMore;
    private ObservableCollection<OrderStatusEntry> _orders = [];

    public OrderStatusPageViewModel()
    {
        Orders = [];
        RevertStatusCommand = new Command<OrderStatusEntry>(async order => await RevertStatusAsync(order));
        ToggleOrderDetailsCommand = new Command<OrderStatusEntry>(async order => await ToggleOrderDetailsAsync(order));
        MarkLineTreatedCommand = new Command<OrderLine>(async line => await MarkLineTreatedAsync(line));
        MarkLineDeliveredCommand = new Command<OrderLine>(async line => await MarkLineDeliveredAsync(line));
        ShowMoreCommand = new Command(ShowMoreOrders);
    }

    public ObservableCollection<OrderStatusEntry> Orders
    {
        get => _orders;
        private set => SetProperty(ref _orders, value);
    }

    public IReadOnlyList<string> AvailableStatuses => _availableStatuses;

    public ICommand RevertStatusCommand { get; }

    public ICommand ToggleOrderDetailsCommand { get; }

    public ICommand MarkLineTreatedCommand { get; }

    public ICommand MarkLineDeliveredCommand { get; }

    public ICommand ShowMoreCommand { get; }

    public string? Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value) && !_hasLoaded)
            {
                _ = InitializeAsync();
            }
        }
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

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                if (string.IsNullOrWhiteSpace(_searchQuery))
                {
                    _isShowingLimitedOrders = true;
                }

                ApplyFilters();
            }
        }
    }

    public bool CanShowMore
    {
        get => _canShowMore;
        private set => SetProperty(ref _canShowMore, value);
    }

    public async Task InitializeAsync()
    {
        if (_hasLoaded)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Status))
        {
            return;
        }

        _hasLoaded = true;
        await LoadStatusAsync().ConfigureAwait(false);
    }

    private async Task LoadStatusAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            IsBusy = true;
            PageTitle = $"Commandes : {Status}";
            Subtitle = "Commandes : en cours de chargement...";
        });

        try
        {
            var endpoint = "https://dantecmarket.com/api/mobile/commandesParEtat";
            var request = new OrderStatusRequest { Cd = Status };
            var orders = await _apis
                .PostAsync<OrderStatusRequest, List<OrderByStatus>>(endpoint, request)
                .ConfigureAwait(false);

            _allOrders.Clear();

            var items = (orders ?? new List<OrderByStatus>())
                .Select(order =>
                {
                    var entry = new OrderStatusEntry();
                    entry.PopulateFromOrder(order, Status);
                    return entry;
                })
                .ToList();

            var statusMap = new Dictionary<OrderStatusEntry, string>();

            foreach (var item in items)
            {
                statusMap[item] = item.CurrentStatus;
                AttachStatusHandler(item);
                _allOrders.Add(item);
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _lastKnownStatuses.Clear();

                foreach (var (order, status) in statusMap)
                {
                    _lastKnownStatuses[order] = status;
                }
                _isShowingLimitedOrders = true;
            });

            ApplyFilters();
        }
        catch (TaskCanceledException)
        {
            await ShowLoadErrorAsync("Le chargement a expiré. Veuillez vérifier votre connexion.");
        }
        catch (HttpRequestException)
        {
            await ShowLoadErrorAsync("Impossible de récupérer les commandes pour cet état.");
        }
        catch (Exception)
        {
            await ShowLoadErrorAsync("Une erreur inattendue est survenue pendant le chargement des commandes.");
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    private void AttachStatusHandler(OrderStatusEntry order)
    {
        order.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(OrderStatusEntry.CurrentStatus))
            {
                _ = OnOrderStatusChangedAsync(order);
            }
        };
    }

    private async Task OnOrderStatusChangedAsync(OrderStatusEntry order)
    {
        if (_statusUpdatesInProgress.Contains(order))
        {
            return;
        }

        var newStatus = order.CurrentStatus;
        var previousStatus = _lastKnownStatuses.TryGetValue(order, out var last) ? last : string.Empty;

        if (string.IsNullOrWhiteSpace(newStatus) || string.Equals(newStatus, previousStatus, StringComparison.Ordinal))
        {
            return;
        }

        await UpdateOrderStatusAsync(order, newStatus, isReverting: false);
    }

    private async Task RevertStatusAsync(OrderStatusEntry? order)
    {
        if (order?.PreviousStatus == null)
        {
            return;
        }

        await UpdateOrderStatusAsync(order, order.PreviousStatus, isReverting: true);
    }

    private async Task<bool> UpdateOrderStatusAsync(OrderStatusEntry order, string newStatus, bool isReverting)
    {
        if (string.IsNullOrWhiteSpace(newStatus))
        {
            return false;
        }

        var previousStatus = _lastKnownStatuses.TryGetValue(order, out var last) ? last : order.CurrentStatus;

        if (string.Equals(previousStatus, newStatus, StringComparison.Ordinal))
        {
            if (isReverting)
            {
                order.ClearPreviousStatus();
            }

            return false;
        }

        var endpoint = "https://dantecmarket.com/api/mobile/updateEtat";
        var request = new UpdateOrderStatusRequest
        {
            Id = order.OrderId,
            Etat = newStatus
        };

        var orderStateEndpoint = "https://dantecmarket.com/api/mobile/updateEtat";
        var normalizedState = NormalizeOrderStateForApi(newStatus);
        OrderDetailsResponse? updatedOrder = null;

        _statusUpdatesInProgress.Add(order);

        try
        {
            var success = await _apis.PostBoolAsync(endpoint, request).ConfigureAwait(false);

            if (!success)
            {
                await ShowLoadErrorAsync("Impossible de modifier l'état de cette commande.");
                await ResetOrderStatusAsync(order, previousStatus);
                return false;
            }

            if (!string.IsNullOrEmpty(normalizedState))
            {
                var orderStateRequest = new ChangeOrderStateRequest
                {
                    Id = order.OrderId,
                    Etat = normalizedState
                };

                updatedOrder = await _apis
                    .PostAsync<ChangeOrderStateRequest, OrderDetailsResponse>(orderStateEndpoint, orderStateRequest)
                    .ConfigureAwait(false);

                if (updatedOrder is null)
                {
                    await ShowLoadErrorAsync("Impossible de synchroniser l'état de la commande.");
                    await ResetOrderStatusAsync(order, previousStatus);
                    return false;
                }
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (isReverting)
                {
                    order.ClearPreviousStatus();
                }
                else
                {
                    order.RememberPreviousStatus(previousStatus);
                }

                SetOrderStatusSilently(order, newStatus);
                _lastKnownStatuses[order] = newStatus;

                if (updatedOrder?.LesCommandes is { Count: > 0 })
                {
                    SyncOrderLines(order, updatedOrder.LesCommandes);
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
        finally
        {
            _statusUpdatesInProgress.Remove(order);
        }

        await ResetOrderStatusAsync(order, previousStatus);
        return false;
    }

    private async Task ResetOrderStatusAsync(OrderStatusEntry order, string status)
    {
        await MainThread.InvokeOnMainThreadAsync(() => SetOrderStatusSilently(order, status));
    }

    private void SetOrderStatusSilently(OrderStatusEntry order, string status)
    {
        _statusUpdatesInProgress.Add(order);
        order.CurrentStatus = status;
        _statusUpdatesInProgress.Remove(order);
    }

    private async Task ToggleOrderDetailsAsync(OrderStatusEntry? order)
    {
        if (order is null)
        {
            return;
        }
        try
        {
            var isOpening = !order.IsExpanded;
            order.IsExpanded = isOpening;

            if (!isOpening)
            {
                return;
            }

            var isAlreadyInProgress = string.Equals(order.CurrentStatus, "En cours de traitement", StringComparison.OrdinalIgnoreCase);
            var isAlreadyCompleted = string.Equals(order.CurrentStatus, "Traitée", StringComparison.OrdinalIgnoreCase);

            if (!isAlreadyInProgress && !isAlreadyCompleted)
            {
                await UpdateOrderStatusAsync(order, "En cours de traitement", isReverting: false);
            }

            await LoadOrderDetailsAsync(order);
        }
        catch (Exception)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                order.IsExpanded = false;
                order.DetailsError = "Une erreur inattendue empêche l'affichage des détails.";
                order.IsLoadingDetails = false;
            });
        }
    }

    private async Task LoadOrderDetailsAsync(OrderStatusEntry order)
    {
        if (order.IsLoadingDetails || order.HasLoadedDetails)
        {
            return;
        }

        order.IsLoadingDetails = true;
        order.DetailsError = null;

        try
        {
            var endpoint = "https://dantecmarket.com/api/mobile/commandeDetails";
            var request = new OrderDetailsRequest { Id = order.OrderId };
            var details = await _apis
                .PostAsync<OrderDetailsRequest, OrderDetailsResponse>(endpoint, request)
                .ConfigureAwait(false);

            var lines = details?.LesCommandes ?? new List<OrderLine>();

            var isDeliveredOrder = string.Equals(order.CurrentStatus, "Livrée", StringComparison.OrdinalIgnoreCase);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                order.OrderLines.Clear();

                foreach (var line in lines)
                {
                    line.OrderId = order.OrderId;

                    if (isDeliveredOrder)
                    {
                        line.Traite = true;
                        line.Livree = true;
                    }

                    order.OrderLines.Add(line);
                }

                order.HasLoadedDetails = true;
            });

            await CheckAndUpdateOrderCompletionAsync(order).ConfigureAwait(false);
            await CheckAndUpdateOrderDeliveryAsync(order).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                order.DetailsError = "Le chargement des détails a expiré.";
            });
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                order.DetailsError = "Impossible de récupérer les détails de cette commande.";
            });
        }
        catch (Exception)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                order.DetailsError = "Une erreur inattendue empêche l'affichage des détails.";
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => order.IsLoadingDetails = false);
        }
    }

    private async Task MarkLineTreatedAsync(OrderLine? line)
    {
        if (line is null || line.Traite)
        {
            return;
        }

        var order = Orders.FirstOrDefault(o => o.OrderId == line.OrderId);

        if (order is null)
        {
            return;
        }

        var endpoint = "https://dantecmarket.com/api/mobile/changerEtatCommander";
        var request = new ChangeOrderLineStateRequest
        {
            Id = line.Id,
            Etat = NormalizeOrderStateForApi("Traitée") ?? "Traitée"
        };

        try
        {
            var success = await _apis.PostBoolAsync(endpoint, request).ConfigureAwait(false);

            if (!success)
            {
                await ShowLoadErrorAsync("Impossible de marquer ce produit comme traité.");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() => line.Traite = true);
            await CheckAndUpdateOrderCompletionAsync(order).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            await ShowLoadErrorAsync("La mise à jour du produit a expiré. Veuillez réessayer.");
        }
        catch (HttpRequestException)
        {
            await ShowLoadErrorAsync("Impossible de mettre à jour ce produit.");
        }
        catch (Exception)
        {
            await ShowLoadErrorAsync("Une erreur inattendue empêche la mise à jour de ce produit.");
        }
    }

    private async Task MarkLineDeliveredAsync(OrderLine? line)
    {
        if (line is null || line.Livree)
        {
            return;
        }

        var order = Orders.FirstOrDefault(o => o.OrderId == line.OrderId);

        if (order is null)
        {
            return;
        }

        var endpoint = "https://dantecmarket.com/api/mobile/changerEtatCommander";
        var request = new ChangeOrderLineStateRequest
        {
            Id = line.Id,
            Etat = NormalizeOrderStateForApi("Livrée") ?? "Livrée"
        };

        try
        {
            var success = await _apis.PostBoolAsync(endpoint, request).ConfigureAwait(false);

            if (!success)
            {
                await ShowLoadErrorAsync("Impossible de marquer ce produit comme livré.");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                line.Traite = true;
                line.Livree = true;
            });

            await CheckAndUpdateOrderCompletionAsync(order).ConfigureAwait(false);
            await CheckAndUpdateOrderDeliveryAsync(order).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            await ShowLoadErrorAsync("La mise à jour du produit a expiré. Veuillez réessayer.");
        }
        catch (HttpRequestException)
        {
            await ShowLoadErrorAsync("Impossible de mettre à jour ce produit.");
        }
        catch (Exception)
        {
            await ShowLoadErrorAsync("Une erreur inattendue empêche la mise à jour de ce produit.");
        }
    }

    private async Task CheckAndUpdateOrderCompletionAsync(OrderStatusEntry order)
    {
        if (order.OrderLines.Count == 0)
        {
            return;
        }

        if (!order.OrderLines.All(line => line.Traite))
        {
            return;
        }

        if (string.Equals(order.CurrentStatus, "Livrée", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(order.CurrentStatus, "Traitée", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await UpdateOrderStatusAsync(order, "Traitée", isReverting: false);
    }

    private async Task CheckAndUpdateOrderDeliveryAsync(OrderStatusEntry order)
    {
        if (order.OrderLines.Count == 0)
        {
            return;
        }

        if (!order.OrderLines.All(line => line.Livree))
        {
            return;
        }

        if (string.Equals(order.CurrentStatus, "Livrée", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await UpdateOrderStatusAsync(order, "Livrée", isReverting: false);
    }

    private void ApplyFilters()
    {
        IEnumerable<OrderStatusEntry> filteredOrders = _allOrders;

        var query = SearchQuery?.Trim();

        if (!string.IsNullOrWhiteSpace(query))
        {
            filteredOrders = filteredOrders.Where(order => order.MatchesQuery(query));
            CanShowMore = false;
        }
        else
        {
            var today = DateTime.Today;
            var todaysOrders = filteredOrders
                .Where(order => order.PickupDate?.Date == today)
                .OrderByDescending(order => order.OrderDate)
                .ToList();

            CanShowMore = _isShowingLimitedOrders && todaysOrders.Count > 3;

            filteredOrders = _isShowingLimitedOrders
                ? todaysOrders.Take(3)
                : todaysOrders;
        }

        var finalOrders = filteredOrders.ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Orders = new ObservableCollection<OrderStatusEntry>(finalOrders);
            Subtitle = $"Commandes : {finalOrders.Count}";
        });
    }

    private void ShowMoreOrders()
    {
        if (!CanShowMore)
        {
            return;
        }

        _isShowingLimitedOrders = false;
        ApplyFilters();
    }

    private static string? NormalizeOrderStateForApi(string status)
    {
        if (string.Equals(status, "Traitée", StringComparison.OrdinalIgnoreCase))
        {
            return "traite";
        }

        if (string.Equals(status, "Livrée", StringComparison.OrdinalIgnoreCase))
        {
            return "livre";
        }

        return null;
    }

    private static void SyncOrderLines(OrderStatusEntry order, IEnumerable<OrderLine> updatedLines)
    {
        var existingLines = order.OrderLines.ToDictionary(line => line.Id);

        foreach (var updatedLine in updatedLines)
        {
            updatedLine.OrderId = order.OrderId;

            if (existingLines.TryGetValue(updatedLine.Id, out var line))
            {
                line.Traite = line.Traite || updatedLine.Traite;
                line.Livree = line.Livree || updatedLine.Livree;
                line.NomProduit = updatedLine.NomProduit;
                line.Quantite = updatedLine.Quantite;
                line.PrixRetenu = updatedLine.PrixRetenu;
                line.ProduitId = updatedLine.ProduitId;
                line.NoteDonnee = updatedLine.NoteDonnee;
            }
            else
            {
                order.OrderLines.Add(updatedLine);
            }
        }
    }

    private static async Task ShowLoadErrorAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DialogService.DisplayAlertAsync("Erreur", message, "OK");
        });
    }

}
