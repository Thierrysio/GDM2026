using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GDM2026.Models;

public class OrderStatusDisplay : INotifyPropertyChanged
{
    private int _count;

    public OrderStatusDisplay(string status, int count)
    {
        Status = status;
        _count = count;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Status { get; }

    public int Count
    {
        get => _count;
        set
        {
            if (SetProperty(ref _count, value))
            {
                OnPropertyChanged(nameof(DisplayCount));
            }
        }
    }

    public string DisplayCount => $"{Count} commande(s)";

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
