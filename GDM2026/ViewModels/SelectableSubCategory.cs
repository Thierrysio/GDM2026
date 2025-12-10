using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GDM2026.Models;

namespace GDM2026.ViewModels;

public class SelectableSubCategory : INotifyPropertyChanged
{
    private bool _isSelected;

    public SelectableSubCategory(SubCategory subCategory)
    {
        Id = subCategory.Id;
        Name = subCategory.DisplayName;
        Description = subCategory.ParentDisplayName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Id { get; }

    public string Name { get; }

    public string Description { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    protected void SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
        {
            return;
        }

        backingStore = value;
        OnPropertyChanged(propertyName);
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
