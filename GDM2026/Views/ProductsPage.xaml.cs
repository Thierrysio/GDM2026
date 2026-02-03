using GDM2026.Models;
using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Net;

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

        if (sender is not Picker picker) return;

        // Ignorer si aucune sélection valide
        if (picker.SelectedIndex < 0) return;

        if (picker.SelectedItem is not ProductCatalogItem selectedProduct)
        {
            return;
        }

        _isNavigating = true;

        try
        {
            Debug.WriteLine($"[PRODUCTS PAGE] Navigation vers produit: {selectedProduct.DisplayName} (ID: {selectedProduct.Id})");

            if (Shell.Current != null)
            {
                var productName = WebUtility.UrlEncode(selectedProduct.DisplayName ?? string.Empty);
                var route = $"{nameof(ProductsEditPage)}?ProductId={selectedProduct.Id}&ProductName={productName}";
                await Shell.Current.GoToAsync(route, animate: false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PRODUCTS PAGE] Erreur navigation : {ex}");
        }
        finally
        {
            // Réinitialise le picker après navigation
            picker.SelectedIndex = -1;
            _isNavigating = false;
        }
    }
}
