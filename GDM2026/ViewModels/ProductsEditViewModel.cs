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

    private bool _categoriesLoaded;
    private bool _isCategoryLoading;
    private bool _isCategorySelectionEnabled = true;
    private SubCategory? _selectedCategory;
    private string _categoryStatusMessage = "Sélectionnez une catégorie pour ce produit.";

    private bool _imageLibraryLoaded;
    private bool _isImageLibraryLoading;
    private string _imageLibraryMessage = "Sélectionnez une image ou utilisez la recherche.";
    private string _imageSearchTerm = string.Empty;
    private string _selectedImageName = "Aucune image sélectionnée.";
    private string? _selectedImageUrl;
    private AdminImage? _selectedLibraryImage;

    public ProductsEditViewModel()
    {
        VisibleProducts = new ObservableCollection<ProductCatalogItem>();
        AvailableCategories = new ObservableCollection<SubCategory>();
        ImageLibrary = new ObservableCollection<AdminImage>();
        FilteredImageLibrary = new ObservableCollection<AdminImage>();
        SearchProductsCommand = new Command(async () => await SearchAsync());
        LoadMoreCommand = new Command(async () => await LoadMoreAsync());
        ProductSelectionChangedCommand = new Command<SelectionChangedEventArgs>(OnProductSelectionChanged);
        SelectProductCommand = new Command<ProductCatalogItem?>(SelectProduct);
        SaveChangesCommand = new Command(async () => await SaveSelectionAsync(), CanSaveSelection);
        ResetFormCommand = new Command(ResetFormToSelection);
    }

    public ObservableCollection<ProductCatalogItem> VisibleProducts { get; }

    public ObservableCollection<SubCategory> AvailableCategories { get; }

    public ObservableCollection<AdminImage> ImageLibrary { get; }

    public ObservableCollection<AdminImage> FilteredImageLibrary { get; }

    public ICommand SearchProductsCommand { get; }

    public ICommand LoadMoreCommand { get; }

    public ICommand ProductSelectionChangedCommand { get; }

    public ICommand SelectProductCommand { get; }

    public ICommand SaveChangesCommand { get; }

    public ICommand ResetFormCommand { get; }

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

    public bool IsNameMissing => string.IsNullOrWhiteSpace(EditProductName);

    public bool IsShortDescriptionMissing => string.IsNullOrWhiteSpace(EditShortDescription);

    public bool IsFullDescriptionMissing => string.IsNullOrWhiteSpace(EditFullDescription);

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
        set
        {
            if (SetProperty(ref _editShortDescription, value))
            {
                RefreshSaveAvailability();
            }
        }
    }

    public string EditFullDescription
    {
        get => _editFullDescription;
        set
        {
            if (SetProperty(ref _editFullDescription, value))
            {
                RefreshSaveAvailability();
            }
        }
    }

    public string EditCategory
    {
        get => _editCategory;
        set
        {
            if (SetProperty(ref _editCategory, value))
            {
                RefreshSaveAvailability();
            }
        }
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
        set
        {
            if (SetProperty(ref _editStockText, value))
            {
                RefreshSaveAvailability();
            }
        }
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

    public bool IsCategoryMissing => SelectedCategory is null && string.IsNullOrWhiteSpace(EditCategory);

    public bool IsPriceMissing => string.IsNullOrWhiteSpace(EditPriceText);

    public bool IsQuantityMissing => string.IsNullOrWhiteSpace(EditStockText);

    public bool IsCategoryLoading
    {
        get => _isCategoryLoading;
        set => SetProperty(ref _isCategoryLoading, value);
    }

    public bool IsCategorySelectionEnabled
    {
        get => _isCategorySelectionEnabled;
        set => SetProperty(ref _isCategorySelectionEnabled, value);
    }

    public string CategoryStatusMessage
    {
        get => _categoryStatusMessage;
        set => SetProperty(ref _categoryStatusMessage, value);
    }

    public SubCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                if (value is not null)
                {
                    EditCategory = value.Name ?? string.Empty;
                    CategoryStatusMessage = $"Catégorie sélectionnée : {value.DisplayName}";
                }
                else
                {
                    CategoryStatusMessage = "Sélectionnez une catégorie pour ce produit.";
                }

                RefreshSaveAvailability();
            }
        }
    }

    public bool IsImageMissing => string.IsNullOrWhiteSpace(_selectedImageUrl);

    public bool IsImageLibraryLoading
    {
        get => _isImageLibraryLoading;
        set => SetProperty(ref _isImageLibraryLoading, value);
    }

    public string ImageLibraryMessage
    {
        get => _imageLibraryMessage;
        set => SetProperty(ref _imageLibraryMessage, value);
    }

    public string ImageSearchTerm
    {
        get => _imageSearchTerm;
        set
        {
            if (SetProperty(ref _imageSearchTerm, value))
            {
                RefreshImageLibraryFilter();
            }
        }
    }

    public AdminImage? SelectedLibraryImage
    {
        get => _selectedLibraryImage;
        set
        {
            if (SetProperty(ref _selectedLibraryImage, value))
            {
                ApplyImageSelection(value);
            }
        }
    }

    public string SelectedImageName
    {
        get => _selectedImageName;
        set => SetProperty(ref _selectedImageName, value);
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

        await LoadCategoriesAsync().ConfigureAwait(false);
        await EnsureImageLibraryLoadedAsync().ConfigureAwait(false);
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

    private async Task LoadCategoriesAsync()
    {
        if (_categoriesLoaded || IsCategoryLoading)
        {
            return;
        }

        try
        {
            IsCategoryLoading = true;
            IsCategorySelectionEnabled = false;
            CategoryStatusMessage = "Chargement des catégories...";

            var categories = await _apis.GetListAsync<SubCategory>("/api/crud/categorie/list").ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AvailableCategories.Clear();
                foreach (var category in categories.OrderBy(c => c.Name))
                {
                    AvailableCategories.Add(category);
                }

                _categoriesLoaded = true;
                CategoryStatusMessage = AvailableCategories.Count == 0
                    ? "Aucune catégorie disponible."
                    : "Sélectionnez une catégorie puis renseignez le reste du formulaire.";
                RefreshSaveAvailability();
            });
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = "Impossible de charger les catégories.";
            Debug.WriteLine($"[PRODUCTS_EDIT] Erreur HTTP (catégories) : {ex}");
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = "Une erreur est survenue lors du chargement des catégories.";
            Debug.WriteLine($"[PRODUCTS_EDIT] Erreur inattendue (catégories) : {ex}");
        }
        finally
        {
            IsCategoryLoading = false;
            IsCategorySelectionEnabled = true;
        }
    }

    private async Task EnsureImageLibraryLoadedAsync()
    {
        if (_imageLibraryLoaded || IsImageLibraryLoading)
        {
            return;
        }

        await LoadImageLibraryAsync();
    }

    private async Task LoadImageLibraryAsync()
    {
        try
        {
            IsImageLibraryLoading = true;
            ImageLibraryMessage = "Chargement de la bibliothèque d'images";

            var images = await _apis.GetListAsync<AdminImage>("/api/crud/images/list").ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ImageLibrary.Clear();
                foreach (var image in images)
                {
                    ImageLibrary.Add(image);
                }

                _imageLibraryLoaded = true;
                ImageLibraryMessage = ImageLibrary.Count == 0
                    ? "Aucune image disponible dans l'admin."
                    : "Sélectionnez une image ou utilisez la recherche.";
                RefreshImageLibraryFilter();
            });
        }
        catch (HttpRequestException ex)
        {
            ImageLibraryMessage = "Impossible de charger la bibliothèque d'images.";
            Debug.WriteLine($"[PRODUCTS_EDIT] Erreur HTTP (images) : {ex}");
        }
        catch (Exception ex)
        {
            ImageLibraryMessage = "Erreur lors du chargement des images.";
            Debug.WriteLine($"[PRODUCTS_EDIT] Erreur inattendue (images) : {ex}");
        }
        finally
        {
            IsImageLibraryLoading = false;
        }
    }

    private void RefreshImageLibraryFilter()
    {
        var hasSearch = !string.IsNullOrWhiteSpace(ImageSearchTerm);
        var normalized = ImageSearchTerm?.Trim().ToLowerInvariant();

        IEnumerable<AdminImage> source = ImageLibrary;
        if (hasSearch)
        {
            source = source.Where(img =>
                (!string.IsNullOrWhiteSpace(img.DisplayName) && img.DisplayName.ToLowerInvariant().Contains(normalized)) ||
                (!string.IsNullOrWhiteSpace(img.Url) && img.Url.ToLowerInvariant().Contains(normalized)));
        }

        FilteredImageLibrary.Clear();
        foreach (var image in source)
        {
            FilteredImageLibrary.Add(image);
        }

        if (hasSearch)
        {
            ImageLibraryMessage = FilteredImageLibrary.Count == 0
                ? "Aucune image ne correspond à cette recherche."
                : $"{FilteredImageLibrary.Count} résultat(s) pour \"{ImageSearchTerm}\".";
        }
        else if (ImageLibrary.Count > 0)
        {
            ImageLibraryMessage = "Sélectionnez une image ou utilisez la recherche.";
        }
    }

    private void ApplyImageSelection(AdminImage? image)
    {
        if (image is null)
        {
            _selectedImageUrl = null;
            SelectedImageName = "Aucune image sélectionnée.";
            RefreshSaveAvailability();
            return;
        }

        _selectedImageUrl = image.Url;
        SelectedImageName = $"Image sélectionnée : {image.DisplayName}";
        RefreshSaveAvailability();
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
            SelectedProduct.Categorie = SelectedCategory?.Name
                ?? (string.IsNullOrWhiteSpace(EditCategory) ? SelectedProduct.Categorie : EditCategory);
            SelectedProduct.Prix = price;
            SelectedProduct.Stock = stock;

            if (!string.IsNullOrWhiteSpace(_selectedImageUrl))
            {
                SelectedProduct.ImageUrl = _selectedImageUrl;
                SelectedProduct.Images = new List<string> { _selectedImageUrl };
            }

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

    private void OnProductSelectionChanged(SelectionChangedEventArgs? args)
    {
        if (args?.CurrentSelection?.FirstOrDefault() is ProductCatalogItem selected)
        {
            SelectedProduct = selected;
            return;
        }

        SelectedProduct = null;
    }

    private void SelectProduct(ProductCatalogItem? product)
    {
        SelectedProduct = product;
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
            SelectedCategory = null;
            ApplyImageSelection(null);
            return;
        }

        EditProductName = product.Nom ?? string.Empty;
        EditShortDescription = product.DescriptionCourte ?? string.Empty;
        EditFullDescription = product.DescriptionLongue ?? string.Empty;
        EditCategory = product.Categorie ?? string.Empty;
        EditPriceText = product.Prix > 0 ? product.Prix.ToString("0.##", CultureInfo.GetCultureInfo("fr-FR")) : string.Empty;
        EditStockText = product.Stock?.ToString() ?? string.Empty;
        EditStatusMessage = $"Modification de {product.DisplayName}.";

        SelectedCategory = AvailableCategories.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.Name)
            && !string.IsNullOrWhiteSpace(product.Categorie)
            && string.Equals(c.Name, product.Categorie, StringComparison.OrdinalIgnoreCase));

        _selectedImageUrl = product.PrimaryImage;
        if (ImageLibrary.Any() && !string.IsNullOrWhiteSpace(_selectedImageUrl))
        {
            SelectedLibraryImage = ImageLibrary.FirstOrDefault(img => string.Equals(img.Url, _selectedImageUrl, StringComparison.OrdinalIgnoreCase));
        }

        SelectedImageName = string.IsNullOrWhiteSpace(_selectedImageUrl)
            ? "Aucune image sélectionnée."
            : $"Image sélectionnée : {_selectedImageUrl}";

        RefreshImageLibraryFilter();
    }

    private bool CanSaveSelection()
    {
        return SelectedProduct is not null
            && !IsSaving
            && !IsCategoryLoading
            && !string.IsNullOrWhiteSpace(EditProductName)
            && !string.IsNullOrWhiteSpace(EditShortDescription)
            && !string.IsNullOrWhiteSpace(EditFullDescription)
            && !IsCategoryMissing
            && !string.IsNullOrWhiteSpace(EditPriceText)
            && !string.IsNullOrWhiteSpace(EditStockText)
            && !IsImageMissing;
    }

    private void RefreshSaveAvailability()
    {
        OnPropertyChanged(nameof(IsNameMissing));
        OnPropertyChanged(nameof(IsShortDescriptionMissing));
        OnPropertyChanged(nameof(IsFullDescriptionMissing));
        OnPropertyChanged(nameof(IsCategoryMissing));
        OnPropertyChanged(nameof(IsPriceMissing));
        OnPropertyChanged(nameof(IsQuantityMissing));
        OnPropertyChanged(nameof(IsImageMissing));
        (SaveChangesCommand as Command)?.ChangeCanExecute();
    }

    private void ResetFormToSelection()
    {
        PrepareEditForm(SelectedProduct);
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
