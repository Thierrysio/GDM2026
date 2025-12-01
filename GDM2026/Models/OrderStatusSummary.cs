using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GDM2026.Models;

public class OrderStatusSummary : INotifyPropertyChanged
{
    private readonly ObservableCollection<OrderByStatus> _orders = new();
    private bool _isExpanded;
    private bool _isLoadingOrders;
    private bool _ordersLoaded;
    private string? _ordersError;

    public OrderStatusSummary(string status, int count)
    {
        Status = status;
        Count = count;

        _orders.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(HasOrders));
            OnPropertyChanged(nameof(OrdersPlaceholder));
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Status { get; }

    public int Count { get; }

    public ObservableCollection<OrderByStatus> Orders => _orders;

    public bool HasOrders => _orders.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(OrdersPlaceholder));
            }
        }
    }

    public bool IsLoadingOrders
    {
        get => _isLoadingOrders;
        set
        {
            if (SetProperty(ref _isLoadingOrders, value))
            {
                OnPropertyChanged(nameof(OrdersPlaceholder));
            }
        }
    }

    public bool OrdersLoaded
    {
        get => _ordersLoaded;
        set
        {
            if (SetProperty(ref _ordersLoaded, value))
            {
                OnPropertyChanged(nameof(OrdersPlaceholder));
            }
        }
    }

    public string? OrdersError
    {
        get => _ordersError;
        set
        {
            if (SetProperty(ref _ordersError, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(OrdersError);

    public string OrdersPlaceholder
    {
        get
        {
            if (IsLoadingOrders)
            {
                return "Chargement des commandes...";
            }

            if (!OrdersLoaded)
            {
                return "Cliquer pour afficher les commandes concernées.";
            }

            return HasOrders ? string.Empty : "Aucune commande pour ce statut.";
        }
    }

    public void ResetOrders()
    {
        _orders.Clear();
        OrdersLoaded = false;
        OrdersError = null;
        IsExpanded = false;
    }

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
