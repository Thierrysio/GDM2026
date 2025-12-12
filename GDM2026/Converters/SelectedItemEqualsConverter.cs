using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace GDM2026.Converters;

public class SelectedItemEqualsConverter : IMultiValueConverter
{
    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2)
        {
            return false;
        }

        var selectedItem = values[0];
        var currentItem = values[1];

        if (selectedItem is null || currentItem is null)
        {
            return false;
        }

        return ReferenceEquals(selectedItem, currentItem) || selectedItem.Equals(currentItem);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
