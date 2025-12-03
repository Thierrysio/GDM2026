using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GDM2026.Models;

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
