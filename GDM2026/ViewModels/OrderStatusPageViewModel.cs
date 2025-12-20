using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public partial class OrderStatusPageViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private readonly IReadOnlyList<string> _availableStatuses =
    [
        "Confirmée",
        "En cours de traitement",
        "Traitée",
        "Livrée",
        "A confirmer"
    ];

    private readonly ObservableCollection<ReservationStatusDisplay> _reservationStatuses = [];
    private readonly Dictionary<OrderStatusEntry, string> _lastKnownStatuses = [];
    private readonly HashSet<OrderStatusEntry> _statusUpdatesInProgress = [];
    private readonly List<OrderStatusEntry> _allOrders = [];

    // ✅ FIX : bon type de delegate pour PropertyChanged
    private readonly Dictionary<OrderStatusEntry, System.ComponentModel.PropertyChangedEventHandler> _statusHandlers = [];

    private DateTime _startDate = DateTime.Today;
    private DateTime _endDate = DateTime.Today;
    private bool _hasInitialized;
    private bool _isReservationMode;
    private string? _status;
    private string _pageTitle = string.Empty;
    private string _subtitle = string.Empty;
    private string _searchQuery = string.Empty;

    private bool _hasAppliedFilters;

    private bool _isShowingLimitedOrders = true;
    private bool _canShowMore;
    private int _displayedReservationsCount = 5;
    private const int ReservationPageSize = 5;

    private ObservableCollection<OrderStatusEntry> _orders = [];

    // PERF : debounce recherche
    private CancellationTokenSource? _searchCts;

    // PERF : évite plusieurs chargements simultanés
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    public OrderStatusPageViewModel()
    {
        Orders = [];

        RevertStatusCommand = new Command<OrderStatusEntry>(async order => await RevertStatusAsync(order));
        ToggleOrderDetailsCommand = new Command<OrderStatusEntry>(async order => await ToggleOrderDetailsAsync(order));
        MarkLineTreatedCommand = new Command<OrderLine>(async line => await MarkLineTreatedAsync(line));
        MarkLineDeliveredCommand = new Command<OrderLine>(async line => await MarkLineDeliveredAsync(line));
        IncreaseLineQuantityCommand = new Command<OrderLine>(async line => await IncreaseLineQuantityAsync(line));
        DecreaseLineQuantityCommand = new Command<OrderLine>(async line => await DecreaseLineQuantityAsync(line));
        ShowMoreCommand = new Command(ShowMoreOrders);

        SelectReservationStatusCommand = new Command<ReservationStatusDisplay>(async status => await OnReservationStatusSelectedAsync(status));
        ApplyReservationFiltersCommand = new Command(async () => await ReloadWithFiltersAsync());
        DeleteReservationCommand = new Command<OrderStatusEntry>(async order => await ConfirmAndDeleteReservationAsync(order));
    }

    public ObservableCollection<OrderStatusEntry> Orders
    {
        get => _orders;
        private set => SetProperty(ref _orders, value);
    }

    public ObservableCollection<ReservationStatusDisplay> ReservationStatuses => _reservationStatuses;

    public IReadOnlyList<string> AvailableStatuses => _availableStatuses;

    public ICommand RevertStatusCommand { get; }
    public ICommand ToggleOrderDetailsCommand { get; }
    public ICommand MarkLineTreatedCommand { get; }
    public ICommand MarkLineDeliveredCommand { get; }
    public ICommand IncreaseLineQuantityCommand { get; }
    public ICommand DecreaseLineQuantityCommand { get; }
    public ICommand ShowMoreCommand { get; }
    public ICommand SelectReservationStatusCommand { get; }
    public ICommand ApplyReservationFiltersCommand { get; }
    public ICommand DeleteReservationCommand { get; }

    public string? Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
                MarkFiltersPending();
        }
    }

    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                EnsureValidDateRange();
                MarkFiltersPending();
            }
        }
    }

    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            if (SetProperty(ref _endDate, value))
            {
                EnsureValidDateRange();
                MarkFiltersPending();
            }
        }
    }

    public bool IsReservationMode
    {
        get => _isReservationMode;
        set
        {
            if (SetProperty(ref _isReservationMode, value))
            {
                EnsureReservationStatuses();
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
            if (!SetProperty(ref _searchQuery, value))
                return;

            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                _isShowingLimitedOrders = true;
            }

            _ = DebouncedApplyFiltersAsync();
        }
    }

    public bool CanShowMore
    {
        get => _canShowMore;
        private set => SetProperty(ref _canShowMore, value);
    }

    public async Task InitializeAsync()
    {
        if (_hasInitialized)
            return;

        _hasInitialized = true;

        if (!await EnsureSessionAsync().ConfigureAwait(false))
            return;

        EnsureReservationStatuses();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            PageTitle = "Réservations";
            Subtitle = "Choisissez une période et un état puis lancez le chargement.";
        });
    }

    private async Task<bool> EnsureSessionAsync()
    {
        var hasSession = await _sessionService.LoadAsync().ConfigureAwait(false);

        if (!hasSession || !_sessionService.IsAuthenticated || string.IsNullOrWhiteSpace(_sessionService.AuthToken))
        {
            await RedirectToLoginAsync().ConfigureAwait(false);
            return false;
        }

        _apis.SetBearerToken(_sessionService.AuthToken);
        return true;
    }

    private static Task RedirectToLoginAsync()
    {
        if (Shell.Current == null)
            return Task.CompletedTask;

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current != null)
                await Shell.Current.GoToAsync($"//{nameof(MainPage)}", animate: false);
        });
    }

    private async Task LoadStatusAsync()
    {
        await _loadSemaphore.WaitAsync().ConfigureAwait(false);

        try
        {
            // Réinitialiser le compteur de pagination pour les réservations
            _displayedReservationsCount = ReservationPageSize;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsBusy = true;
                PageTitle = IsReservationMode ? $"Réservations : {Status}" : $"Commandes : {Status}";
                Subtitle = IsReservationMode
                    ? $"{Status} · période du {StartDate:dd/MM/yyyy} au {EndDate:dd/MM/yyyy}"
                    : "Commandes : en cours de chargement...";
            });

            _hasAppliedFilters = true;

            List<OrderByStatus> orders;

            if (IsReservationMode)
            {
                var endpoint = "/reserver/commandes/etat";
                var request = new ReservationStatusRequest
                {
                    Etat = Status,
                    DateDebut = StartDate.ToString("yyyy-MM-dd"),
                    DateFin = EndDate.ToString("yyyy-MM-dd")
                };

                var reservationOrders = await _apis
                    .PostAsync<ReservationStatusRequest, List<ReservationOrder>>(endpoint, request)
                    .ConfigureAwait(false) ?? new List<ReservationOrder>();

                orders = reservationOrders
                    .Select(order => MapReservationOrder(order, Status))
                    .ToList();
            }
            else
            {
                var endpoint = "https://dantecmarket.com/api/mobile/commandesParEtat";
                var request = new OrderStatusRequest { Cd = Status };
                orders = await _apis
                    .PostAsync<OrderStatusRequest, List<OrderByStatus>>(endpoint, request)
                    .ConfigureAwait(false) ?? new List<OrderByStatus>();
            }

            // Nettoyage handlers pour éviter accumulation
            DetachAllStatusHandlers();

            _allOrders.Clear();

            var items = (orders ?? new List<OrderByStatus>())
                .Select(order =>
                {
                    var entry = new OrderStatusEntry();
                    entry.PopulateFromOrder(order, Status);
                    return entry;
                })
                .ToList();

            var statusMap = new Dictionary<OrderStatusEntry, string>(items.Count);

            foreach (var item in items)
            {
                statusMap[item] = item.CurrentStatus;
                AttachStatusHandler(item);
                _allOrders.Add(item);
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _lastKnownStatuses.Clear();
                foreach (var (order, st) in statusMap)
                    _lastKnownStatuses[order] = st;

                _isShowingLimitedOrders = true;
            });

            await ApplyFiltersAsync().ConfigureAwait(false);
            UpdateSelectedReservationCount();
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
            _loadSemaphore.Release();
        }
    }

    private void AttachStatusHandler(OrderStatusEntry order)
    {
        if (_statusHandlers.ContainsKey(order))
            return;

        System.ComponentModel.PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == nameof(OrderStatusEntry.CurrentStatus))
            {
                _ = OnOrderStatusChangedAsync(order);
            }
        };

        _statusHandlers[order] = handler;
        order.PropertyChanged += handler;
    }

    private void DetachAllStatusHandlers()
    {
        foreach (var kvp in _statusHandlers.ToList())
        {
            kvp.Key.PropertyChanged -= kvp.Value;
        }

        _statusHandlers.Clear();
    }

    private async Task OnOrderStatusChangedAsync(OrderStatusEntry order)
    {
        if (_statusUpdatesInProgress.Contains(order))
            return;

        if (IsReservationMode)
            return;

        var newStatus = order.CurrentStatus;
        var previousStatus = _lastKnownStatuses.TryGetValue(order, out var last) ? last : string.Empty;

        if (string.IsNullOrWhiteSpace(newStatus) ||
            string.Equals(newStatus, previousStatus, StringComparison.Ordinal))
        {
            return;
        }

        await UpdateOrderStatusAsync(order, newStatus, isReverting: false);
    }

    private async Task RevertStatusAsync(OrderStatusEntry? order)
    {
        if (order?.PreviousStatus == null)
            return;

        await UpdateOrderStatusAsync(order, order.PreviousStatus, isReverting: true);
    }

    private async Task<bool> UpdateOrderStatusAsync(OrderStatusEntry order, string newStatus, bool isReverting)
    {
        if (string.IsNullOrWhiteSpace(newStatus))
            return false;

        var previousStatus = _lastKnownStatuses.TryGetValue(order, out var last) ? last : order.CurrentStatus;

        if (string.Equals(previousStatus, newStatus, StringComparison.Ordinal))
        {
            if (isReverting) order.ClearPreviousStatus();
            return false;
        }

        var endpoint = "https://dantecmarket.com/api/mobile/updateEtat";
        var request = new UpdateOrderStatusRequest { Id = order.OrderId, Etat = newStatus };

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
                var orderStateRequest = new ChangeOrderStateRequest { Id = order.OrderId, Etat = normalizedState };
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
                if (isReverting) order.ClearPreviousStatus();
                else order.RememberPreviousStatus(previousStatus);

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

    private Task ResetOrderStatusAsync(OrderStatusEntry order, string status)
        => MainThread.InvokeOnMainThreadAsync(() => SetOrderStatusSilently(order, status));

    private void SetOrderStatusSilently(OrderStatusEntry order, string status)
    {
        _statusUpdatesInProgress.Add(order);
        order.CurrentStatus = status;
        _statusUpdatesInProgress.Remove(order);
    }

    private async Task ToggleOrderDetailsAsync(OrderStatusEntry? order)
    {
        if (order is null) return;

        try
        {
            var isOpening = !order.IsExpanded;
            order.IsExpanded = isOpening;

            if (!isOpening) return;

            var statusAlreadyInProgress = string.Equals(order.CurrentStatus, "En cours de traitement", StringComparison.OrdinalIgnoreCase);
            var statusAlreadyCompleted = string.Equals(order.CurrentStatus, "Traitée", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(order.CurrentStatus, "Livrée", StringComparison.OrdinalIgnoreCase);

            if (!statusAlreadyInProgress && !statusAlreadyCompleted)
            {
                var isAlreadyInProgress = string.Equals(order.CurrentStatus, "En cours de traitement", StringComparison.OrdinalIgnoreCase);
                var isAlreadyCompleted = string.Equals(order.CurrentStatus, "Traitée", StringComparison.OrdinalIgnoreCase);

                if (!isAlreadyInProgress && !isAlreadyCompleted)
                {
                    await UpdateOrderStatusAsync(order, "En cours de traitement", isReverting: false);
                }
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
            return;

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
            await MainThread.InvokeOnMainThreadAsync(() => order.DetailsError = "Le chargement des détails a expiré.");
        }
        catch (HttpRequestException)
        {
            await MainThread.InvokeOnMainThreadAsync(() => order.DetailsError = "Impossible de récupérer les détails de cette commande.");
        }
        catch (Exception)
        {
            await MainThread.InvokeOnMainThreadAsync(() => order.DetailsError = "Une erreur inattendue empêche l'affichage des détails.");
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => order.IsLoadingDetails = false);
        }
    }

    private async Task MarkLineTreatedAsync(OrderLine? line)
    {
        if (line is null || line.Traite) return;

        var order = Orders.FirstOrDefault(o => o.OrderId == line.OrderId);
        if (order is null) return;

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
        if (line is null || line.Livree) return;

        var order = Orders.FirstOrDefault(o => o.OrderId == line.OrderId);
        if (order is null) return;

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

            // Mise à jour du stock : envoyer l'ID du produit et la quantité livrée
            await UpdateProductStockAsync(line.ProduitId, line.Quantite).ConfigureAwait(false);

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

    private async Task UpdateProductStockAsync(int produitId, int quantite)
    {
        if (produitId <= 0 || quantite <= 0) return;

        var stockEndpoint = "https://dantecmarket.com/api/mobile/updateStock";
        var stockRequest = new UpdateStockRequest
        {
            ProduitId = produitId,
            Quantite = quantite
        };

        try
        {
            await _apis.PostBoolAsync(stockEndpoint, stockRequest).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Log silencieux : la mise à jour du stock est secondaire
            // Le produit a déjà été marqué comme livré
        }
    }

    private async Task IncreaseLineQuantityAsync(OrderLine? line)
    {
        if (line is null) return;

        var order = Orders.FirstOrDefault(o => o.OrderId == line.OrderId);
        if (order is null) return;

        // Ne pas permettre la modification si la commande est déjà livrée
        if (string.Equals(order.CurrentStatus, "Livrée", StringComparison.OrdinalIgnoreCase)) return;

        var newQuantity = line.Quantite + 1;
        await UpdateLineQuantityAsync(line, order, newQuantity).ConfigureAwait(false);
    }

    private async Task DecreaseLineQuantityAsync(OrderLine? line)
    {
        if (line is null) return;

        var order = Orders.FirstOrDefault(o => o.OrderId == line.OrderId);
        if (order is null) return;

        // Ne pas permettre la modification si la commande est déjà livrée
        if (string.Equals(order.CurrentStatus, "Livrée", StringComparison.OrdinalIgnoreCase)) return;

        var newQuantity = line.Quantite - 1;

        if (newQuantity <= 0)
        {
            // Confirmer la suppression de la ligne
            var confirmed = await MainThread.InvokeOnMainThreadAsync(() =>
                DialogService.DisplayConfirmationAsync(
                    "Retirer le produit",
                    $"Voulez-vous retirer \"{line.LeProduit?.NomProduit ?? "ce produit"}\" de la réservation ?",
                    "Oui",
                    "Non"));

            if (!confirmed) return;

            await RemoveLineFromOrderAsync(line, order).ConfigureAwait(false);
        }
        else
        {
            await UpdateLineQuantityAsync(line, order, newQuantity).ConfigureAwait(false);
        }
    }

    private async Task UpdateLineQuantityAsync(OrderLine line, OrderStatusEntry order, int newQuantity)
    {
        var previousQuantity = line.Quantite;

        var endpoint = "https://dantecmarket.com/api/mobile/updateQuantiteCommander";
        var request = new UpdateLineQuantityRequest
        {
            Id = line.Id,
            Quantite = newQuantity
        };

        try
        {
            var success = await _apis.PostBoolAsync(endpoint, request).ConfigureAwait(false);
            if (!success)
            {
                await ShowLoadErrorAsync("Impossible de modifier la quantité.");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                line.Quantite = newQuantity;
                // Recalculer le montant total de la commande
                RecalculateOrderTotal(order);
            });
        }
        catch (TaskCanceledException)
        {
            await ShowLoadErrorAsync("La mise à jour a expiré. Veuillez réessayer.");
        }
        catch (HttpRequestException)
        {
            await ShowLoadErrorAsync("Impossible de mettre à jour la quantité.");
        }
        catch (Exception)
        {
            await ShowLoadErrorAsync("Une erreur inattendue empêche la mise à jour de la quantité.");
        }
    }

    private async Task RemoveLineFromOrderAsync(OrderLine line, OrderStatusEntry order)
    {
        var endpoint = "https://dantecmarket.com/api/mobile/supprimerLigneCommande";
        var request = new { Id = line.Id };

        try
        {
            var success = await _apis.PostBoolAsync(endpoint, request).ConfigureAwait(false);
            if (!success)
            {
                await ShowLoadErrorAsync("Impossible de retirer ce produit de la réservation.");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                order.OrderLines.Remove(line);
                // Recalculer le montant total de la commande
                RecalculateOrderTotal(order);
            });
        }
        catch (TaskCanceledException)
        {
            await ShowLoadErrorAsync("La suppression a expiré. Veuillez réessayer.");
        }
        catch (HttpRequestException)
        {
            await ShowLoadErrorAsync("Impossible de retirer ce produit.");
        }
        catch (Exception)
        {
            await ShowLoadErrorAsync("Une erreur inattendue empêche la suppression du produit.");
        }
    }

    private static void RecalculateOrderTotal(OrderStatusEntry order)
    {
        var newTotal = order.OrderLines.Sum(l => l.PrixRetenu * l.Quantite);
        order.TotalAmount = newTotal;
    }

    private async Task CheckAndUpdateOrderCompletionAsync(OrderStatusEntry order)
    {
        if (order.OrderLines.Count == 0) return;
        if (!order.OrderLines.All(l => l.Traite)) return;

        if (string.Equals(order.CurrentStatus, "Livrée", StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(order.CurrentStatus, "Traitée", StringComparison.OrdinalIgnoreCase)) return;

        await UpdateOrderStatusAsync(order, "Traitée", isReverting: false);
    }

    private async Task CheckAndUpdateOrderDeliveryAsync(OrderStatusEntry order)
    {
        if (order.OrderLines.Count == 0) return;
        if (!order.OrderLines.All(l => l.Livree)) return;

        if (string.Equals(order.CurrentStatus, "Livrée", StringComparison.OrdinalIgnoreCase)) return;

        await UpdateOrderStatusAsync(order, "Livrée", isReverting: false);
    }

    private Task DebouncedApplyFiltersAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                await ApplyFiltersAsync().ConfigureAwait(false);
            }
            catch { /* ignore */ }
        });
    }

    private async Task ApplyFiltersAsync()
    {
        var query = (SearchQuery ?? string.Empty).Trim();
        var isQuery = !string.IsNullOrWhiteSpace(query);

        List<OrderStatusEntry> finalOrders;

        if (isQuery)
        {
            finalOrders = _allOrders
                .Where(o => o.MatchesQuery(query))
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToList();
            await MainThread.InvokeOnMainThreadAsync(() => CanShowMore = false);
        }
        else if (IsReservationMode)
        {
            var start = StartDate.Date;
            var end = EndDate.Date;

            var filteredReservations = _allOrders
                .Where(o => !o.PickupDate.HasValue || (o.PickupDate.Value.Date >= start && o.PickupDate.Value.Date <= end))
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            var totalCount = filteredReservations.Count;
            var canShowMore = totalCount > _displayedReservationsCount;

            finalOrders = filteredReservations
                .Take(_displayedReservationsCount)
                .ToList();

            await MainThread.InvokeOnMainThreadAsync(() => CanShowMore = canShowMore);
        }
        else
        {
            var today = DateTime.Today;

            var todaysOrders = _allOrders
                .Where(o => o.PickupDate?.Date == today)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            var canShowMore = _isShowingLimitedOrders && todaysOrders.Count > 3;

            finalOrders = _isShowingLimitedOrders
                ? todaysOrders.Take(3).ToList()
                : todaysOrders;

            await MainThread.InvokeOnMainThreadAsync(() => CanShowMore = canShowMore);
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Orders.Clear();
            foreach (var o in finalOrders)
                Orders.Add(o);

            Subtitle = IsReservationMode
                ? _hasAppliedFilters
                    ? $"Réservations affichées : {finalOrders.Count} sur {_allOrders.Count}"
                    : "Choisissez une période, un état puis lancez la recherche"
                : $"Commandes : {finalOrders.Count}";
        });
    }

    private void ShowMoreOrders()
    {
        if (!CanShowMore) return;

        if (IsReservationMode)
        {
            _displayedReservationsCount += ReservationPageSize;
        }
        else
        {
            _isShowingLimitedOrders = false;
        }

        _ = ApplyFiltersAsync();
    }

    private static string? NormalizeOrderStateForApi(string status)
    {
        if (string.Equals(status, "Traitée", StringComparison.OrdinalIgnoreCase)) return "traite";
        if (string.Equals(status, "Livrée", StringComparison.OrdinalIgnoreCase)) return "livre";
        return null;
    }

    private async Task ConfirmAndDeleteReservationAsync(OrderStatusEntry? order)
    {
        if (order is null || !IsReservationMode) return;

        var isConfirmed = await RequestReservationDeletionAsync(order.OrderId).ConfigureAwait(false);
        if (!isConfirmed) return;

        const string endpoint = "/reserver/commandes/supprimer";
        var request = new DeleteReservationRequest { Id = order.OrderId };

        try
        {
            var success = await _apis.PostBoolAsync(endpoint, request).ConfigureAwait(false);
            if (!success)
            {
                await ShowLoadErrorAsync("Impossible de supprimer cette réservation.");
                return;
            }

            await RemoveReservationAsync(order).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            await ShowLoadErrorAsync("La suppression a expiré. Veuillez vérifier votre connexion.");
        }
        catch (HttpRequestException)
        {
            await ShowLoadErrorAsync("Impossible de supprimer cette réservation.");
        }
        catch (Exception)
        {
            await ShowLoadErrorAsync("Une erreur inattendue empêche la suppression de cette réservation.");
        }
    }

    private Task<bool> RequestReservationDeletionAsync(int orderId)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
            DialogService.DisplayConfirmationAsync(
                "Confirmation",
                $"Voulez-vous annuler la réservation #{orderId} ?",
                "Oui",
                "Non"));
    }

    private async Task RemoveReservationAsync(OrderStatusEntry order)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _allOrders.Remove(order);
            _lastKnownStatuses.Remove(order);
            _statusUpdatesInProgress.Remove(order);
            Orders.Remove(order);
        });

        UpdateSelectedReservationCount();
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

    private void EnsureReservationStatuses()
    {
        if (!IsReservationMode || ReservationStatuses.Count > 0) return;

        foreach (var status in _availableStatuses)
            ReservationStatuses.Add(new ReservationStatusDisplay(status));
    }

    private async Task OnReservationStatusSelectedAsync(ReservationStatusDisplay? status)
    {
        if (status is null) return;

        foreach (var tile in ReservationStatuses)
            tile.IsSelected = ReferenceEquals(tile, status);

        Status = status.Status;
    }

    public Task ReloadWithFiltersAsync()
    {
        if (string.IsNullOrWhiteSpace(Status))
            return Task.CompletedTask;

        return EnsureInitializedAndLoadAsync();
    }

    private async Task EnsureInitializedAndLoadAsync()
    {
        if (!_hasInitialized)
            await InitializeAsync().ConfigureAwait(false);

        await LoadStatusAsync().ConfigureAwait(false);
    }

    private void MarkFiltersPending()
    {
        _hasAppliedFilters = false;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Subtitle = "Choisissez une période, un état puis lancez la recherche";
        });
    }

    private void EnsureValidDateRange()
    {
        if (EndDate < StartDate)
            EndDate = StartDate;
    }

    private static OrderByStatus MapReservationOrder(ReservationOrder order, string? fallbackStatus)
    {
        var reservation = order.Reservations?.FirstOrDefault();

        var mapped = new OrderByStatus
        {
            Id = order.Id,
            Etat = string.IsNullOrWhiteSpace(order.Etat) ? fallbackStatus : order.Etat,
            Valider = order.Valider,
            MontantTotal = order.MontantTotal,
            DateCommande = TryParseDate(order.DateCommande),
            PlanningDetails = reservation?.Planning,
            Jour = reservation?.Date
        };

        foreach (var line in order.LignesCommande ?? new List<ReservationOrderLine>())
        {
            mapped.LesCommandes.Add(new OrderLine
            {
                Id = line.Id,
                Quantite = line.Quantite,
                PrixRetenu = line.PrixRetenu,
                LeProduit = new ProductSummary { NomProduit = line.Produit }
            });
        }

        return mapped;
    }

    private void UpdateSelectedReservationCount()
    {
        if (!IsReservationMode) return;

        var selected = ReservationStatuses.FirstOrDefault(t => t.IsSelected)
                       ?? ReservationStatuses.FirstOrDefault(t => string.Equals(t.Status, Status, StringComparison.OrdinalIgnoreCase));

        if (selected is null) return;

        selected.Count = Orders?.Count ?? 0;
    }

    private static DateTime TryParseDate(string? dateText)
    {
        if (DateTime.TryParse(dateText, out var parsed))
            return parsed;

        return DateTime.Now;
    }

    private static async Task ShowLoadErrorAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DialogService.DisplayAlertAsync("Erreur", message, "OK");
        });
    }
}
