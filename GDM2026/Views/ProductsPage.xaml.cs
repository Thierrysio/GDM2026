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

        // Vérifier que la liste n'est pas vide
        if (_viewModel.SortedProducts.Count == 0)
        {
            await DisplayAlert("Erreur", "La liste des produits est vide", "OK");
            return;
        }

        // Récupérer le produit sélectionné via le ViewModel
        if (picker.SelectedIndex >= _viewModel.SortedProducts.Count)
        {
            await DisplayAlert("Erreur", $"Index {picker.SelectedIndex} hors limites (max: {_viewModel.SortedProducts.Count - 1})", "OK");
            return;
        }

        var selectedProduct = _viewModel.SortedProducts[picker.SelectedIndex];
        if (selectedProduct == null)
        {
            await DisplayAlert("Erreur", "Produit null", "OK");
            return;
        }

        _isNavigating = true;

        try
        {
            // Navigation directe sans paramètres pour tester
            await Shell.Current.GoToAsync(nameof(ProductsEditPage));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur Navigation", ex.Message, "OK");
        }
        finally
        {
            picker.SelectedIndex = -1;
            _isNavigating = false;
        }
    }
}
