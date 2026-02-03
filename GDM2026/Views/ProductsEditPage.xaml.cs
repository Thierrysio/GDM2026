using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GDM2026;

[QueryProperty(nameof(ProductId), "ProductId")]
[QueryProperty(nameof(ProductName), "ProductName")]
public partial class ProductsEditPage : ContentPage, IQueryAttributable
{
    private readonly ProductsEditViewModel _viewModel = new();
    private int? _productId;
    private string? _productName;
    private int? _pendingProductId;
    private bool _isInitialized;

    public int? ProductId
    {
        get => _productId;
        set
        {
            _productId = value;
            _pendingProductId = value;
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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query == null)
        {
            return;
        }

        if (query.TryGetValue("ProductId", out var rawProductId))
        {
            var parsed = ParseProductId(rawProductId);
            if (parsed.HasValue)
            {
                ProductId = parsed;
            }
        }

        if (query.TryGetValue("ProductName", out var rawProductName))
        {
            ProductName = rawProductName?.ToString();
        }

        if (_isInitialized)
        {
            _ = LoadSelectedProductAsync();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _viewModel.InitializeAsync();
            _isInitialized = true;

            await LoadSelectedProductAsync();
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
        _pendingProductId = null;
        _isInitialized = false;
    }

    private async Task LoadSelectedProductAsync()
    {
        if (!_pendingProductId.HasValue || _pendingProductId.Value <= 0)
        {
            return;
        }

        Debug.WriteLine($"[PRODUCTS_EDIT_PAGE] Chargement du produit ID: {_pendingProductId.Value}");
        await _viewModel.LoadAndSelectProductByIdAsync(_pendingProductId.Value);
    }

    private static int? ParseProductId(object rawProductId)
    {
        if (rawProductId is null)
        {
            return null;
        }

        if (rawProductId is int directId)
        {
            return directId;
        }

        if (rawProductId is long longId)
        {
            return (int)longId;
        }

        if (rawProductId is string text && int.TryParse(text, out var parsed))
        {
            return parsed;
        }

        if (int.TryParse(rawProductId.ToString(), out var fallback))
        {
            return fallback;
        }

        return null;
    }
}
