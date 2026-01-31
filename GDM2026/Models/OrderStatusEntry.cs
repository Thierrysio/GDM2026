using Microsoft.Maui.ApplicationModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace GDM2026.Models;

public class OrderStatusEntry : INotifyPropertyChanged
{
    private string _currentStatus = string.Empty;
    private string? _detailsError;
    private bool _hasLoadedDetails;
    private bool _isExpanded;
    private bool _isLoadingDetails;
    private DateTime _orderDate;
    private DateTime? _pickupDate;
    private string? _previousStatus;
    private string? _selectedStatusOption;
    private double _totalAmount;
    private double _loyaltyReduction;
    private int _loyaltyUserId;
    private int _loyaltyCouronnesUsed;
    private bool _notificationSent;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int OrderId { get; set; }

    public string OrderLabel => $"Commande #{OrderId}";

    public string DisplayDate { get; set; } = string.Empty;

    public string DisplayAmount => TotalAmount.ToString("C", CultureInfo.GetCultureInfo("fr-FR"));

    public double TotalAmount
    {
        get => _totalAmount;
        set
        {
            if (SetProperty(ref _totalAmount, value))
            {
                OnPropertyChanged(nameof(DisplayAmount));
                OnPropertyChanged(nameof(DisplayAmountWithReduction));
            }
        }
    }

    /// <summary>
    /// Réduction fidélité appliquée sur cette commande
    /// </summary>
    public double LoyaltyReduction
    {
        get => _loyaltyReduction;
        set
        {
            if (SetProperty(ref _loyaltyReduction, value))
            {
                OnPropertyChanged(nameof(HasLoyaltyReduction));
                OnPropertyChanged(nameof(DisplayLoyaltyReduction));
                OnPropertyChanged(nameof(DisplayAmountWithReduction));
                OnPropertyChanged(nameof(FinalAmount));
            }
        }
    }

    /// <summary>
    /// ID de l'utilisateur qui a utilisé ses points fidélité
    /// </summary>
    public int LoyaltyUserId
    {
        get => _loyaltyUserId;
        set => SetProperty(ref _loyaltyUserId, value);
    }

    /// <summary>
    /// Nombre de couronnes utilisées
    /// </summary>
    public int LoyaltyCouronnesUsed
    {
        get => _loyaltyCouronnesUsed;
        set
        {
            if (SetProperty(ref _loyaltyCouronnesUsed, value))
            {
                OnPropertyChanged(nameof(DisplayLoyaltyCouronnes));
            }
        }
    }

    /// <summary>
    /// Indique si une réduction fidélité a été appliquée
    /// </summary>
    public bool HasLoyaltyReduction => LoyaltyReduction > 0;

    /// <summary>
    /// Affichage de la réduction fidélité
    /// </summary>
    public string DisplayLoyaltyReduction => $"-{LoyaltyReduction:C}";

    /// <summary>
    /// Affichage des couronnes utilisées
    /// </summary>
    public string DisplayLoyaltyCouronnes => $"{LoyaltyCouronnesUsed} couronnes utilisées";

    /// <summary>
    /// Montant final après réduction fidélité
    /// </summary>
    public double FinalAmount => Math.Max(0, TotalAmount - LoyaltyReduction);

    /// <summary>
    /// Affichage du montant avec réduction
    /// </summary>
    public string DisplayAmountWithReduction => HasLoyaltyReduction
        ? $"{FinalAmount:C} (après réduction)"
        : DisplayAmount;

    /// <summary>
    /// Indique si le bouton fidélité peut être utilisé (pas encore livrée)
    /// </summary>
    public bool CanUseLoyalty => !string.Equals(CurrentStatus, "Livrée", StringComparison.OrdinalIgnoreCase)
                                 && !HasLoyaltyReduction;

    /// <summary>
    /// Indique si une notification a été envoyée pour cette commande
    /// </summary>
    public bool NotificationSent
    {
        get => _notificationSent;
        set
        {
            if (SetProperty(ref _notificationSent, value))
            {
                OnPropertyChanged(nameof(CanSendNotification));
                OnPropertyChanged(nameof(NotificationButtonText));
            }
        }
    }

    /// <summary>
    /// Indique si le bouton de notification peut être affiché (commande traitée)
    /// </summary>
    public bool CanSendNotification => string.Equals(CurrentStatus, "Traitée", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Texte du bouton de notification
    /// </summary>
    public string NotificationButtonText => NotificationSent ? "🔔 Renvoyer" : "🔔 Notifier";

    public ObservableCollection<OrderLine> OrderLines { get; } = [];

    public int? UserId { get; set; }

    public DateTime OrderDate
    {
        get => _orderDate;
        private set => SetProperty(ref _orderDate, value);
    }

    public string CurrentStatus
    {
        get => _currentStatus;
        set
        {
            if (SetProperty(ref _currentStatus, value))
            {
                OnPropertyChanged(nameof(CanUseLoyalty));
                OnPropertyChanged(nameof(CanSendNotification));
            }
        }
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

    public DateTime? PickupDate
    {
        get => _pickupDate;
        private set
        {
            if (SetProperty(ref _pickupDate, value))
            {
                OnPropertyChanged(nameof(HasPickupDate));
                OnPropertyChanged(nameof(PickupDateDisplay));
            }
        }
    }

    public bool HasPickupDate => PickupDate.HasValue;

    public string PickupDateDisplay => PickupDate?.ToString("dddd dd MMMM", CultureInfo.GetCultureInfo("fr-FR"))
        ?? "Jour de retrait non défini";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsLoadingDetails
    {
        get => _isLoadingDetails;
        set => SetProperty(ref _isLoadingDetails, value);
    }

    public bool HasLoadedDetails
    {
        get => _hasLoadedDetails;
        set => SetProperty(ref _hasLoadedDetails, value);
    }

    public string? DetailsError
    {
        get => _detailsError;
        set
        {
            if (SetProperty(ref _detailsError, value))
            {
                OnPropertyChanged(nameof(HasDetailsError));
            }
        }
    }

    public bool HasDetailsError => !string.IsNullOrWhiteSpace(DetailsError);

    public string? SelectedStatusOption
    {
        get => _selectedStatusOption;
        set
        {
            if (SetProperty(ref _selectedStatusOption, value))
            {
                if (!string.IsNullOrWhiteSpace(value) &&
                    !string.Equals(value, CurrentStatus, StringComparison.Ordinal))
                {
                    CurrentStatus = value;
                }

                if (value is not null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (_selectedStatusOption is not null)
                        {
                            _selectedStatusOption = null;
                            OnPropertyChanged(nameof(SelectedStatusOption));
                        }
                    });
                }
            }
        }
    }

    public void PopulateFromOrder(OrderByStatus order, string? fallbackStatus = null)
    {
        OrderId = order.Id;
        OrderDate = order.DateCommande;
        TotalAmount = order.MontantTotal;
        UserId = ResolveUserId(order);
        var status = order.Etat;

        if (string.IsNullOrWhiteSpace(status))
        {
            status = fallbackStatus;
        }

        CurrentStatus = string.IsNullOrWhiteSpace(status)
            ? "État non renseigné"
            : status;
        DisplayDate = order.DateCommande.ToString("dd MMM yyyy - HH:mm", CultureInfo.GetCultureInfo("fr-FR"));
        PickupDate = TryParsePickupDate(order.Jour);
    }

    public void ApplyLoyaltyReduction(int userId, int couronnes, double reduction)
    {
        LoyaltyUserId = userId;
        LoyaltyCouronnesUsed = couronnes;
        LoyaltyReduction = reduction;
    }

    public void RememberPreviousStatus(string status) => PreviousStatus = status;

    public void ClearPreviousStatus() => PreviousStatus = null;

    public bool MatchesQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var comparison = StringComparison.OrdinalIgnoreCase;

        return OrderId.ToString().Contains(query, comparison)
            || OrderLabel.Contains(query, comparison)
            || DisplayDate.Contains(query, comparison)
            || DisplayAmount.Contains(query, comparison)
            || (CurrentStatus?.Contains(query, comparison) ?? false)
            || (HasPickupDate && PickupDateDisplay.Contains(query, comparison));
    }

    private static int? ResolveUserId(OrderByStatus order)
    {
        if (order.UserId is > 0)
        {
            return order.UserId;
        }

        if (order.UserIdFidelite is > 0)
        {
            return order.UserIdFidelite;
        }

        return null;
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
    {
        if (Equals(backingStore, value))
        {
            return false;
        }

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static DateTime? TryParsePickupDate(string? dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText))
        {
            return null;
        }

        return DateTime.TryParse(dateText, out var pickupDate)
            ? pickupDate.Date
            : null;
    }
}
