using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GDM2026.Models;

public class OrderStatusDisplay : INotifyPropertyChanged
{
    private int _count;
    private int _delta;

    public OrderStatusDisplay(string status, int count, int delta = 0)
    {
        Status = status;
        _count = count;
        _delta = delta;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Status { get; }

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    public int Delta
    {
        get => _delta;
        set
        {
            if (SetProperty(ref _delta, value))
            {
                OnPropertyChanged(nameof(DisplayCount));
            }
        }
    }

    public string DisplayCount => Delta != 0 ? $"{Count} ({(Delta > 0 ? "+" : string.Empty)}{Delta})" : Count.ToString();

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
