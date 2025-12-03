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
        "En cours de traitement",
        "Traitée",
        "Livrée",
        "A confirmer"
    };

    private readonly Dictionary<OrderStatusEntry, string> _lastKnownStatuses = new();
    private readonly HashSet<OrderStatusEntry> _statusUpdatesInProgress = new();
    private bool _hasLoaded;
    private string? _status;
    private string _pageTitle = string.Empty;
    private string _subtitle = string.Empty;

    public OrderStatusPageViewModel()
    {
        Orders = new ObservableCollection<OrderStatusEntry>();
        RevertStatusCommand = new Command<OrderStatusEntry>(async order => await RevertStatusAsync(order));
    }

    public ObservableCollection<OrderStatusEntry> Orders { get; }

    public IReadOnlyList<string> AvailableStatuses => _availableStatuses;

    public ICommand RevertStatusCommand { get; }

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

            var items = (orders ?? new List<OrderByStatus>())
                .Select(order =>
                {
                    var entry = new OrderStatusEntry();
                    entry.PopulateFromOrder(order, Status);
                    return entry;
                })
                .ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Orders.Clear();
                _lastKnownStatuses.Clear();

                foreach (var item in items)
                {
                    Orders.Add(item);
                    _lastKnownStatuses[item] = item.CurrentStatus;
                    AttachStatusHandler(item);
                }

                Subtitle = $"Commandes : {Orders.Count}";
            });
        }
        catch (TaskCanceledException)
        {
            await ShowLoadErrorAsync("Le chargement a expiré. Veuillez vérifier votre connexion.");
        }
        catch (HttpRequestException)
        {
            await ShowLoadErrorAsync("Impossible de récupérer les commandes pour cet état.");
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

    private static async Task ShowLoadErrorAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DialogService.DisplayAlertAsync("Erreur", message, "OK");
        });
    }

}
