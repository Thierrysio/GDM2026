using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GDM2026.Models;

public class ReservationStatusDisplay : INotifyPropertyChanged
{
    private bool _isSelected;
    private int _count;

    public ReservationStatusDisplay(string status)
    {
        Status = status;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Status { get; }

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string DisplayCount => Count == 0 ? "" : $"{Count} commande(s)";

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
