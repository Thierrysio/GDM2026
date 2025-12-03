using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace GDM2026.Models;

public class OrderStatusEntry : INotifyPropertyChanged
{
    private string _currentStatus = string.Empty;
    private string? _previousStatus;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int OrderId { get; set; }

    public string OrderLabel => $"Commande #{OrderId}";

    public string DisplayDate { get; set; } = string.Empty;

    public string DisplayAmount { get; set; } = string.Empty;

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

    public void PopulateFromOrder(OrderByStatus order, string? fallbackStatus = null)
    {
        OrderId = order.Id;
        CurrentStatus = order.Etat ?? fallbackStatus ?? string.Empty;
        DisplayDate = order.DateCommande.ToString("dd MMM yyyy - HH:mm", CultureInfo.GetCultureInfo("fr-FR"));
        DisplayAmount = order.MontantTotal.ToString("C", CultureInfo.GetCultureInfo("fr-FR"));
    }

    public void RememberPreviousStatus(string status) => PreviousStatus = status;

    public void ClearPreviousStatus() => PreviousStatus = null;

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
}
