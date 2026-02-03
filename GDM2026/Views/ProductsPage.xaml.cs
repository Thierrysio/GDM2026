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

        if (sender is not Picker picker) return;

        // Ignorer si aucune sélection valide
        if (picker.SelectedIndex < 0) return;

        // Récupérer le produit sélectionné via le ViewModel
        if (picker.SelectedIndex >= _viewModel.SortedProducts.Count) return;

        var selectedProduct = _viewModel.SortedProducts[picker.SelectedIndex];
        if (selectedProduct == null) return;

        _isNavigating = true;

        try
        {
            Debug.WriteLine($"[PRODUCTS PAGE] Navigation vers produit: {selectedProduct.DisplayName} (ID: {selectedProduct.Id})");

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
        }
        finally
        {
            // Réinitialise le picker après navigation
            picker.SelectedIndex = -1;
            _isNavigating = false;
        }
    }
}
