using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class ProductsEditViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionLoaded;
    private bool _isSearching;
    private bool _hasMore = false;
    private string _searchText = string.Empty;
    private string _statusMessage = "Saisissez du texte puis appuyez sur la loupe pour lancer une recherche.";

    private bool _productsLoaded;
    private bool _isLoadingProducts;
    private readonly List<ProductCatalogItem> _productCache = new();

    private ProductCatalogItem? _selectedProduct;
    private string _editProductName = string.Empty;
    private string _editShortDescription = string.Empty;
    private string _editFullDescription = string.Empty;
    private string _editCategory = string.Empty;
    private string _editPriceText = string.Empty;
    private string _editStockText = string.Empty;
    private string _editStatusMessage = "Sélectionnez un produit pour afficher le formulaire de modification.";
    private bool _isSaving;

    public ProductsEditViewModel()
    {
        VisibleProducts = new ObservableCollection<ProductCatalogItem>();
        SearchProductsCommand = new Command(async () => await SearchAsync());
        LoadMoreCommand = new Command(async () => await LoadMoreAsync());
        SaveChangesCommand = new Command(async () => await SaveSelectionAsync(), CanSaveSelection);
    }

    public ObservableCollection<ProductCatalogItem> VisibleProducts { get; }

    public ICommand SearchProductsCommand { get; }

    public ICommand LoadMoreCommand { get; }

    public ICommand SaveChangesCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool HasMore
    {
        get => _hasMore;
        set => SetProperty(ref _hasMore, value);
    }

    public ProductCatalogItem? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                PrepareEditForm(value);
                OnPropertyChanged(nameof(IsEditFormVisible));
                RefreshSaveAvailability();
            }
        }
    }

    public bool IsEditFormVisible => SelectedProduct is not null;

    public string EditProductName
    {
        get => _editProductName;
        set
        {
            if (SetProperty(ref _editProductName, value))
            {
                RefreshSaveAvailability();
            }
        }
    }

    public string EditShortDescription
    {
        get => _editShortDescription;
        set => SetProperty(ref _editShortDescription, value);
    }

    public string EditFullDescription
    {
        get => _editFullDescription;
        set => SetProperty(ref _editFullDescription, value);
    }

    public string EditCategory
    {
        get => _editCategory;
        set => SetProperty(ref _editCategory, value);
    }

    public string EditPriceText
    {
        get => _editPriceText;
        set
        {
            if (SetProperty(ref _editPriceText, value))
            {
                RefreshSaveAvailability();
            }
        }
    }

    public string EditStockText
    {
        get => _editStockText;
        set => SetProperty(ref _editStockText, value);
    }

    public string EditStatusMessage
    {
        get => _editStatusMessage;
        set => SetProperty(ref _editStatusMessage, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        set
        {
            if (SetProperty(ref _isSaving, value))
            {
                RefreshSaveAvailability();
            }
        }
    }

    public async Task InitializeAsync()
    {
        if (_sessionLoaded)
        {
            return;
        }

        await _sessionService.LoadAsync().ConfigureAwait(false);
        _apis.SetBearerToken(_sessionService.AuthToken);
        _sessionLoaded = true;
    }

    private async Task SearchAsync()
    {
        if (_isSearching)
        {
            return;
        }

        _isSearching = true;

        try
        {
            VisibleProducts.Clear();
            HasMore = false;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                StatusMessage = "Saisissez du texte puis appuyez sur la loupe pour lancer une recherche.";
                return;
            }

            StatusMessage = $"Recherche pour '{SearchText}'";

            var products = await EnsureProductsCacheAsync().ConfigureAwait(false);
            var filtered = FilterProducts(products, SearchText).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var item in filtered)
                {
                    VisibleProducts.Add(item);
                }

                StatusMessage = VisibleProducts.Count == 0
                    ? "Aucun produit trouvé."
                    : $"{VisibleProducts.Count} produit(s) trouvé(s).";
            });
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = "Impossible de récupérer les produits.";
            Debug.WriteLine($"[PRODUCTS_EDIT] HTTP error: {ex}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur lors de la recherche.";
            Debug.WriteLine($"[PRODUCTS_EDIT] error: {ex}");
        }
        finally
        {
            _isSearching = false;
        }
    }

    private async Task LoadMoreAsync()
    {
        if (!HasMore || IsBusy)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Chargement supplémentaire";

            var products = await EnsureProductsCacheAsync().ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var item in products)
                {
                    VisibleProducts.Add(item);
                }

                HasMore = false;

                StatusMessage = VisibleProducts.Count == 0
                    ? "Aucun produit n'est disponible."
                    : $"{VisibleProducts.Count} produit(s) affiché(s).";
            });
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = "Impossible de charger plus de produits.";
            Debug.WriteLine($"[PRODUCTS_EDIT] HTTP error: {ex}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur lors du chargement supplémentaire.";
            Debug.WriteLine($"[PRODUCTS_EDIT] error: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<List<ProductCatalogItem>> EnsureProductsCacheAsync()
    {
        if (_productsLoaded && _productCache.Count > 0)
        {
            return _productCache;
        }

        if (_isLoadingProducts)
        {
            return _productCache;
        }

        _isLoadingProducts = true;
        try
        {
            var data = await FetchProductsFromApiAsync().ConfigureAwait(false);

            _productCache.Clear();
            _productCache.AddRange(data);
            _productsLoaded = true;

            return _productCache;
        }
        finally
        {
            _isLoadingProducts = false;
        }
    }

    private async Task<List<ProductCatalogItem>> FetchProductsFromApiAsync()
    {
        List<ProductCatalogItem>? results = null;
        Exception? lastError = null;

        var endpoints = new[]
        {
            "/api/produits",
            "/api/mobile/produits",
            "/api/mobile/GetListProduit"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                var data = await _apis.GetListAsync<ProductCatalogItem>(endpoint).ConfigureAwait(false);
                if (results is null || data.Count > 0)
                {
                    results = data;
                }

                if (data.Count > 0)
                {
                    return data;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                Debug.WriteLine($"[PRODUCTS_EDIT] Endpoint échoué '{endpoint}': {ex.Message}");
            }
        }

        if (results != null)
        {
            return results;
        }

        if (lastError != null)
        {
            throw lastError;
        }

        return new List<ProductCatalogItem>();
    }

    private async Task SaveSelectionAsync()
    {
        if (SelectedProduct is null)
        {
            return;
        }

        if (!CanSaveSelection())
        {
            EditStatusMessage = "Renseignez au moins le nom et le prix du produit.";
            return;
        }

        IsSaving = true;

        try
        {
            var culture = CultureInfo.GetCultureInfo("fr-FR");
            var numberStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;

            var price = double.TryParse(EditPriceText, numberStyles, culture, out var parsedPrice)
                ? parsedPrice
                : 0d;

            int? stock = null;
            if (int.TryParse(EditStockText, NumberStyles.Integer, culture, out var parsedStock))
            {
                stock = parsedStock;
            }

            SelectedProduct.Nom = EditProductName.Trim();
            SelectedProduct.DescriptionCourte = EditShortDescription;
            SelectedProduct.DescriptionLongue = EditFullDescription;
            SelectedProduct.Categorie = string.IsNullOrWhiteSpace(EditCategory) ? SelectedProduct.Categorie : EditCategory;
            SelectedProduct.Prix = price;
            SelectedProduct.Stock = stock;

            EditStatusMessage = "Modifications prêtes à être envoyées (sauvegarde locale uniquement).";
            StatusMessage = $"Produit #{SelectedProduct.Id} prêt pour modification.";
        }
        catch (Exception ex)
        {
            EditStatusMessage = "Erreur lors de la préparation des modifications.";
            Debug.WriteLine($"[PRODUCTS_EDIT] save error: {ex}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void PrepareEditForm(ProductCatalogItem? product)
    {
        if (product is null)
        {
            EditProductName = string.Empty;
            EditShortDescription = string.Empty;
            EditFullDescription = string.Empty;
            EditCategory = string.Empty;
            EditPriceText = string.Empty;
            EditStockText = string.Empty;
            EditStatusMessage = "Sélectionnez un produit pour afficher le formulaire de modification.";
            return;
        }

        EditProductName = product.Nom ?? string.Empty;
        EditShortDescription = product.DescriptionCourte ?? string.Empty;
        EditFullDescription = product.DescriptionLongue ?? string.Empty;
        EditCategory = product.Categorie ?? string.Empty;
        EditPriceText = product.Prix > 0 ? product.Prix.ToString("0.##", CultureInfo.GetCultureInfo("fr-FR")) : string.Empty;
        EditStockText = product.Stock?.ToString() ?? string.Empty;
        EditStatusMessage = $"Modification de {product.DisplayName}.";
    }

    private bool CanSaveSelection()
    {
        return SelectedProduct is not null
            && !IsSaving
            && !string.IsNullOrWhiteSpace(EditProductName)
            && !string.IsNullOrWhiteSpace(EditPriceText);
    }

    private void RefreshSaveAvailability()
    {
        (SaveChangesCommand as Command)?.ChangeCanExecute();
    }

    private static IEnumerable<ProductCatalogItem> FilterProducts(IEnumerable<ProductCatalogItem> products, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return products;
        }

        var normalized = query.Trim().ToLowerInvariant();
        return products.Where(p =>
            (!string.IsNullOrWhiteSpace(p.Nom) && p.Nom.ToLowerInvariant().Contains(normalized)) ||
            (!string.IsNullOrWhiteSpace(p.Description) && p.Description.ToLowerInvariant().Contains(normalized)) ||
            (!string.IsNullOrWhiteSpace(p.Categorie) && p.Categorie.ToLowerInvariant().Contains(normalized)));
    }
}
