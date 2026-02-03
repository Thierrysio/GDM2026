using GDM2026.Models;
using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GDM2026;

public partial class ProductsPage : ContentPage
{
    private readonly ProductsViewModel _viewModel = new();
    private bool _isNavigating;

    public ProductsPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _isNavigating = false;

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PRODUCTS PAGE] Erreur lors de l'initialisation : {ex}");
        }
    }

    private async void OnEditProductClicked(object sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync(nameof(ProductsEditPage), animate: false);
        }
    }

    private async void OnProductPickerSelectedIndexChanged(object sender, EventArgs e)
    {
        if (_isNavigating) return;

        var picker = sender as Picker;
        if (picker?.SelectedItem is ProductCatalogItem selectedProduct)
        {
            _isNavigating = true;

            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "ProductId", selectedProduct.Id },
                    { "ProductName", selectedProduct.DisplayName ?? string.Empty }
                };

                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync(nameof(ProductsEditPage), animate: false, parameters);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PRODUCTS PAGE] Erreur navigation : {ex}");
                _isNavigating = false;
            }
            finally
            {
                // Réinitialise le picker après navigation
                picker.SelectedItem = null;
            }
        }
    }
}
