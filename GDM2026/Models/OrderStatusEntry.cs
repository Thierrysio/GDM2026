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
            }
        }
    }

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
        set => SetProperty(ref _currentStatus, value);
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
        UserId = order.UserId;
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
