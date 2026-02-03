using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;

namespace GDM2026;

[QueryProperty(nameof(ProductId), "ProductId")]
[QueryProperty(nameof(ProductName), "ProductName")]
public partial class ProductsEditPage : ContentPage
{
    private readonly ProductsEditViewModel _viewModel = new();
    private int? _productId;
    private string? _productName;

    public int? ProductId
    {
        get => _productId;
        set
        {
            _productId = value;
            Debug.WriteLine($"[PRODUCTS_EDIT_PAGE] ProductId reçu: {value}");
        }
    }

    public string? ProductName
    {
        get => _productName;
        set
        {
            _productName = value;
            Debug.WriteLine($"[PRODUCTS_EDIT_PAGE] ProductName reçu: {value}");
        }
    }

    public ProductsEditPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _viewModel.InitializeAsync();

            // Si un ProductId a été passé en paramètre, charger ce produit
            if (_productId.HasValue && _productId.Value > 0)
            {
                Debug.WriteLine($"[PRODUCTS_EDIT_PAGE] Chargement du produit ID: {_productId.Value}");
                await _viewModel.LoadAndSelectProductByIdAsync(_productId.Value);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PRODUCTS_EDIT_PAGE] Erreur : {ex}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Réinitialiser les paramètres pour la prochaine navigation
        _productId = null;
        _productName = null;
    }
}
