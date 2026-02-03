using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class ProductsViewModel : BaseViewModel
{
    private const bool ProductLoadingEnabled = true;
    private const string DefaultCreationMessage = "Remplissez le formulaire pour créer un produit.";
    private const int SearchDebounceMs = 300;

    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _imageSearchCts;
    private CancellationTokenSource? _categoryFilterCts;

    private bool _sessionPrepared;
    private bool _hasLoaded;
    private bool _isRefreshing;
    private string _statusMessage = "Chargement du catalogue";
    private string _searchText = string.Empty;

    private bool _isFormVisible;
    private ProductCatalogItem? _selectedProductFromPicker;
    private SubCategory? _selectedCatalogCategory;
    private string _catalogFilterStatus = "Sélectionnez un produit ou filtrez par catégorie";
    private string _newProductName = string.Empty;
    private string _newProductShortDescription = string.Empty;
    private string _newProductFullDescription = string.Empty;
    private string _newProductCategory = string.Empty;
    private string _productPriceText = string.Empty;
    private string _productQuantityText = string.Empty;
    private string _creationStatusMessage = DefaultCreationMessage;
    private Color _creationStatusColor = Colors.Gold;
    private bool _isSubmittingProduct;
    private bool _isSubmitEnabled;

    private bool _categoriesLoaded;
    private bool _isCategoryLoading;
    private bool _isCategorySelectionEnabled = true;
    private string _categoryStatusMessage = "Catégories en cours de chargement";
    private SubCategory? _selectedCategory;

    private bool _imageLibraryLoaded;
    private bool _isImageLibraryLoading;
    private string _imageLibraryMessage = "Sélectionnez une image depuis la bibliothèque.";
    private string _imageSearchTerm = string.Empty;
    private AdminImage? _selectedLibraryImage;
    private string _selectedImageName = "Aucune image sélectionnée.";
    private string? _selectedImageUrl;

    public ProductsViewModel()
    {
        Products = new ObservableCollection<ProductCatalogItem>();
        FilteredProducts = new ObservableCollection<ProductCatalogItem>();
        AvailableCategories = new ObservableCollection<SubCategory>();
        ImageLibrary = new ObservableCollection<AdminImage>();
        FilteredImageLibrary = new ObservableCollection<AdminImage>();

        RefreshCommand = new Command(async () => await LoadProductsAsync(forceRefresh: true));
        ToggleFormCommand = new Command(async () => await ToggleFormAsync());
        SubmitProductCommand = new Command(async () => await SubmitProductAsync(), CanSubmitProduct);
        ResetFormCommand = new Command(ResetForm);
        ClearFiltersCommand = new Command(ClearFilters);

        RefreshSubmitAvailability();
    }

    public ObservableCollection<ProductCatalogItem> Products { get; }

    public ObservableCollection<ProductCatalogItem> FilteredProducts { get; }

    public ObservableCollection<SubCategory> AvailableCategories { get; }

    public ObservableCollection<AdminImage> ImageLibrary { get; }

    public ObservableCollection<AdminImage> FilteredImageLibrary { get; }

    public ICommand RefreshCommand { get; }

    public ICommand ToggleFormCommand { get; }

    public ICommand SubmitProductCommand { get; }

    public ICommand ResetFormCommand { get; }

    public ICommand ClearFiltersCommand { get; }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = ApplyFilterWithDebounceAsync();
            }
        }
    }

    public bool IsFormVisible
    {
        get => _isFormVisible;
        set
        {
            if (SetProperty(ref _isFormVisible, value) && value)
            {
                _ = EnsureImageLibraryLoadedAsync();
            }
        }
    }

    public ObservableCollection<ProductCatalogItem> SortedProducts { get; } = new();

    public ObservableCollection<SubCategory> CatalogCategories { get; } = new();

    public ProductCatalogItem? SelectedProductFromPicker
    {
        get => _selectedProductFromPicker;
        set
        {
            if (SetProperty(ref _selectedProductFromPicker, value) && value is not null)
            {
                ScrollToProduct(value);
            }
        }
    }

    public SubCategory? SelectedCatalogCategory
    {
        get => _selectedCatalogCategory;
        set
        {
            if (SetProperty(ref _selectedCatalogCategory, value))
            {
                _ = ApplyFilterWithDebounceAsync(immediate: true);
            }
        }
    }

    public string CatalogFilterStatus
    {
        get => _catalogFilterStatus;
        set => SetProperty(ref _catalogFilterStatus, value);
    }

    public string NewProductName
    {
        get => _newProductName;
        set
        {
            if (SetProperty(ref _newProductName, value))
            {
                RefreshSubmitAvailability();
            }
        }
    }

    public bool IsNameMissing => string.IsNullOrWhiteSpace(NewProductName);

    public string NewProductShortDescription
    {
        get => _newProductShortDescription;
        set
        {
            if (SetProperty(ref _newProductShortDescription, value))
            {
                RefreshSubmitAvailability();
            }
        }
    }

    public bool IsShortDescriptionMissing => string.IsNullOrWhiteSpace(NewProductShortDescription);

    public string NewProductFullDescription
    {
        get => _newProductFullDescription;
        set
        {
            if (SetProperty(ref _newProductFullDescription, value))
            {
                RefreshSubmitAvailability();
            }
        }
    }

    public bool IsFullDescriptionMissing => string.IsNullOrWhiteSpace(NewProductFullDescription);

    public string NewProductCategory
    {
        get => _newProductCategory;
        set
        {
            if (SetProperty(ref _newProductCategory, value))
            {
                RefreshSubmitAvailability();
            }
        }
    }

    public bool IsCategoryMissing => SelectedCategory is null;

    public string ProductPriceText
    {
        get => _productPriceText;
        set
        {
            if (SetProperty(ref _productPriceText, value))
            {
                RefreshSubmitAvailability();
            }
        }
    }

    public bool IsPriceMissing => string.IsNullOrWhiteSpace(ProductPriceText);

    public string ProductQuantityText
    {
        get => _productQuantityText;
        set
        {
            if (SetProperty(ref _productQuantityText, value))
            {
                RefreshSubmitAvailability();
            }
        }
    }

    public bool IsQuantityMissing => string.IsNullOrWhiteSpace(ProductQuantityText);

    public string CreationStatusMessage
    {
        get => _creationStatusMessage;
        set => SetProperty(ref _creationStatusMessage, value);
    }

    public Color CreationStatusColor
    {
        get => _creationStatusColor;
        set => SetProperty(ref _creationStatusColor, value);
    }

    public bool IsSubmitEnabled
    {
        get => _isSubmitEnabled;
        private set => SetProperty(ref _isSubmitEnabled, value);
    }

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
                NewProductCategory = value?.Name ?? string.Empty;
                CategoryStatusMessage = value is null
                    ? "Sélectionnez une catégorie pour ce produit."
                    : $"Catégorie sélectionnée : {value.DisplayName}";
                RefreshSubmitAvailability();
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
                _ = RefreshImageLibraryFilterWithDebounceAsync();
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
        if (!_sessionPrepared)
        {
            await PrepareSessionAsync();
        }

        if (!_hasLoaded)
        {
            await LoadProductsAsync();
        }
    }

    private async Task PrepareSessionAsync()
    {
        try
        {
            await _sessionService.LoadAsync().ConfigureAwait(false);
            _apis.SetBearerToken(_sessionService.AuthToken);
            _sessionPrepared = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PRODUCTS] Session non préparée : {ex}");
            _sessionPrepared = true;
        }
    }

    private async Task LoadProductsAsync(bool forceRefresh = false)
    {
        if (!ProductLoadingEnabled)
        {
            StatusMessage = "Le chargement du catalogue est désactivé.";
            IsRefreshing = false;
            IsBusy = false;
            Products.Clear();
            FilteredProducts.Clear();
            return;
        }

        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            IsRefreshing = forceRefresh;
            StatusMessage = forceRefresh ? "Actualisation du catalogue" : "Chargement du catalogue";

            var items = await FetchProductsAsync().ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Products.Clear();
                foreach (var item in items)
                {
                    Products.Add(item);
                }

                _hasLoaded = true;
                RefreshSortedProductsAndCategories();
                ApplyFilter();

                if (!Products.Any())
                {
                    StatusMessage = "Aucun produit à afficher pour le moment.";
                    CatalogFilterStatus = "Catalogue vide";
                }
                else if (string.IsNullOrWhiteSpace(SearchText))
                {
                    StatusMessage = $"{Products.Count} produit(s) disponible(s).";
                    CatalogFilterStatus = $"{SortedProducts.Count} produit(s) • {CatalogCategories.Count} catégorie(s)";
                }
            });
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "Chargement annulé.";
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = "Impossible de récupérer les produits.";
            Debug.WriteLine($"[PRODUCTS] Erreur HTTP : {ex}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Une erreur est survenue lors du chargement du catalogue.";
            Debug.WriteLine($"[PRODUCTS] Erreur inattendue : {ex}");
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    private async Task<List<ProductCatalogItem>> FetchProductsAsync()
    {
        List<ProductCatalogItem>? results = null;
        Exception? lastError = null;

        var endpoints = new[]
        {
            "/api/produits",
            "/api/mobile/GetListProduit",
            "/api/mobile/getListProduit",
            "/api/mobile/produits",
            "/api/mobile/catalogue",
            "/api/crud/produit/list",
            "/api/crud/produits/list"
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
                Debug.WriteLine($"[PRODUCTS] Endpoint échoué '{endpoint}': {ex.Message}");
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

    private async Task ApplyFilterWithDebounceAsync(bool immediate = false)
    {
        // Annule la recherche précédente
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            // Debounce sauf si immediate
            if (!immediate)
            {
                await Task.Delay(SearchDebounceMs, token).ConfigureAwait(false);
            }

            if (token.IsCancellationRequested) return;

            // Capture les valeurs actuelles
            var query = SearchText?.Trim();
            var selectedCategoryName = SelectedCatalogCategory?.Name;
            var productsList = Products.ToList();

            // Exécute le filtrage en arrière-plan
            var filtered = await Task.Run(() =>
            {
                IEnumerable<ProductCatalogItem> source = productsList;

                // Filtre par catégorie si sélectionnée
                if (!string.IsNullOrWhiteSpace(selectedCategoryName))
                {
                    source = source.Where(p =>
                        !string.IsNullOrWhiteSpace(p.Categorie) &&
                        p.Categorie.Equals(selectedCategoryName, StringComparison.OrdinalIgnoreCase));
                }

                // Filtre par texte de recherche
                if (!string.IsNullOrWhiteSpace(query))
                {
                    var normalized = query.ToLowerInvariant();
                    source = source.Where(p =>
                        (p.DisplayName?.ToLowerInvariant().Contains(normalized) ?? false) ||
                        (p.Description?.ToLowerInvariant().Contains(normalized) ?? false) ||
                        (p.Categorie?.ToLowerInvariant().Contains(normalized) ?? false));
                }

                return source.ToList();
            }, token).ConfigureAwait(false);

            if (token.IsCancellationRequested) return;

            // Met à jour l'UI sur le thread principal
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                FilteredProducts.Clear();
                foreach (var item in filtered)
                {
                    FilteredProducts.Add(item);
                }
                UpdateFilterStatus(query, selectedCategoryName);
            });
        }
        catch (OperationCanceledException)
        {
            // Recherche annulée, ignorer
        }
    }

    private void ApplyFilter()
    {
        // Version synchrone pour les appels internes (ex: après chargement)
        _ = ApplyFilterWithDebounceAsync(immediate: true);
    }

    private void UpdateFilterStatus(string? query, string? categoryName)
    {
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var hasCategory = !string.IsNullOrWhiteSpace(categoryName);

        if (FilteredProducts.Count == 0)
        {
            StatusMessage = "Aucun produit ne correspond aux critères.";
            CatalogFilterStatus = "Aucun résultat trouvé";
        }
        else if (hasQuery && hasCategory)
        {
            StatusMessage = $"{FilteredProducts.Count} résultat(s) pour \"{query}\" dans {categoryName}.";
            CatalogFilterStatus = $"Filtré par : {categoryName} + recherche";
        }
        else if (hasQuery)
        {
            StatusMessage = $"{FilteredProducts.Count} résultat(s) pour \"{query}\".";
            CatalogFilterStatus = "Résultats de recherche";
        }
        else if (hasCategory)
        {
            StatusMessage = $"{FilteredProducts.Count} produit(s) dans {categoryName}.";
            CatalogFilterStatus = $"Filtré par : {categoryName}";
        }
        else if (Products.Count > 0)
        {
            StatusMessage = $"{Products.Count} produit(s) disponible(s).";
            CatalogFilterStatus = "Tous les produits";
        }
    }

    private void ScrollToProduct(ProductCatalogItem product)
    {
        // Réinitialise les filtres pour afficher le produit
        SearchText = string.Empty;
        SelectedCatalogCategory = null;

        // Sélectionne le produit dans la liste filtrée
        var index = FilteredProducts.IndexOf(product);
        if (index >= 0)
        {
            CatalogFilterStatus = $"Produit sélectionné : {product.DisplayName}";
            StatusMessage = $"Affichage de : {product.DisplayName}";
        }
    }

    private async Task RefreshSortedProductsAndCategoriesAsync()
    {
        var productsList = Products.ToList();

        // Exécute le tri en arrière-plan
        var (sortedProducts, uniqueCategories) = await Task.Run(() =>
        {
            var sorted = productsList
                .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var categories = productsList
                .Where(p => !string.IsNullOrWhiteSpace(p.Categorie))
                .Select(p => p.Categorie!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (sorted, categories);
        }).ConfigureAwait(false);

        // Met à jour l'UI sur le thread principal
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            SortedProducts.Clear();
            foreach (var product in sortedProducts)
            {
                SortedProducts.Add(product);
            }

            CatalogCategories.Clear();
            foreach (var categoryName in uniqueCategories)
            {
                CatalogCategories.Add(new SubCategory { Name = categoryName });
            }
        });
    }

    private void RefreshSortedProductsAndCategories()
    {
        // Lance en arrière-plan sans bloquer
        _ = RefreshSortedProductsAndCategoriesAsync();
    }

    private void ClearFilters()
    {
        // Annule les recherches en cours
        _searchCts?.Cancel();

        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        _selectedCatalogCategory = null;
        OnPropertyChanged(nameof(SelectedCatalogCategory));
        _selectedProductFromPicker = null;
        OnPropertyChanged(nameof(SelectedProductFromPicker));

        // Lance le filtre immédiatement
        _ = ApplyFilterWithDebounceAsync(immediate: true);
        CatalogFilterStatus = Products.Count > 0
            ? $"Filtres réinitialisés • {Products.Count} produit(s)"
            : "Catalogue vide";
    }

    private async Task ToggleFormAsync()
    {
        IsFormVisible = !IsFormVisible;
        if (IsFormVisible)
        {
            await EnsureImageLibraryLoadedAsync();
            await EnsureCategoriesLoadedAsync();
        }
    }

    private async Task EnsureCategoriesLoadedAsync()
    {
        if (_categoriesLoaded || IsCategoryLoading)
        {
            return;
        }

        if (!_sessionPrepared)
        {
            await PrepareSessionAsync();
        }

        await LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
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
                RefreshSubmitAvailability();
            });
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = "Impossible de charger les catégories.";
            Debug.WriteLine($"[PRODUCTS] Erreur HTTP (catégories) : {ex}");
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = "Une erreur est survenue lors du chargement des catégories.";
            Debug.WriteLine($"[PRODUCTS] Erreur inattendue (catégories) : {ex}");
        }
        finally
        {
            IsCategoryLoading = false;
            IsCategorySelectionEnabled = true;
            RefreshSubmitAvailability();
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
            Debug.WriteLine($"[PRODUCTS] Erreur HTTP (images) : {ex}");
        }
        catch (Exception ex)
        {
            ImageLibraryMessage = "Erreur lors du chargement des images.";
            Debug.WriteLine($"[PRODUCTS] Erreur inattendue (images) : {ex}");
        }
        finally
        {
            IsImageLibraryLoading = false;
        }
    }

    private async Task RefreshImageLibraryFilterWithDebounceAsync()
    {
        // Annule la recherche précédente
        _imageSearchCts?.Cancel();
        _imageSearchCts = new CancellationTokenSource();
        var token = _imageSearchCts.Token;

        try
        {
            // Debounce
            await Task.Delay(SearchDebounceMs, token).ConfigureAwait(false);

            if (token.IsCancellationRequested) return;

            var searchTerm = ImageSearchTerm;
            var hasSearch = !string.IsNullOrWhiteSpace(searchTerm);
            var normalized = searchTerm?.Trim().ToLowerInvariant();
            var imageList = ImageLibrary.ToList();

            // Exécute le filtrage en arrière-plan
            var filtered = await Task.Run(() =>
            {
                IEnumerable<AdminImage> source = imageList;
                if (hasSearch && normalized != null)
                {
                    source = source.Where(img =>
                        (!string.IsNullOrWhiteSpace(img.DisplayName) && img.DisplayName.ToLowerInvariant().Contains(normalized)) ||
                        (!string.IsNullOrWhiteSpace(img.Url) && img.Url.ToLowerInvariant().Contains(normalized)));
                }
                return source.ToList();
            }, token).ConfigureAwait(false);

            if (token.IsCancellationRequested) return;

            // Met à jour l'UI sur le thread principal
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                FilteredImageLibrary.Clear();
                foreach (var image in filtered)
                {
                    FilteredImageLibrary.Add(image);
                }

                if (hasSearch)
                {
                    ImageLibraryMessage = FilteredImageLibrary.Count == 0
                        ? "Aucune image ne correspond à cette recherche."
                        : $"{FilteredImageLibrary.Count} résultat(s) pour \"{searchTerm}\".";
                }
                else if (ImageLibrary.Count > 0)
                {
                    ImageLibraryMessage = "Sélectionnez une image ou utilisez la recherche.";
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Recherche annulée, ignorer
        }
    }

    private void RefreshImageLibraryFilter()
    {
        // Version synchrone pour les appels internes
        _ = RefreshImageLibraryFilterWithDebounceAsync();
    }

    private void ApplyImageSelection(AdminImage? image)
    {
        if (image is null)
        {
            _selectedImageUrl = null;
            SelectedImageName = "Aucune image sélectionnée.";
            RefreshSubmitAvailability();
            return;
        }

        _selectedImageUrl = image.Url;
        SelectedImageName = $"Image sélectionnée : {image.DisplayName}";
        CreationStatusColor = Colors.Gold;
        CreationStatusMessage = "Image prête pour la création.";
        RefreshSubmitAvailability();
    }

    private bool CanSubmitProduct()
    {
        return !_isSubmittingProduct
            && !IsCategoryLoading
            && !string.IsNullOrWhiteSpace(NewProductName)
            && !string.IsNullOrWhiteSpace(NewProductShortDescription)
            && !string.IsNullOrWhiteSpace(NewProductFullDescription)
            && SelectedCategory is not null
            && !string.IsNullOrWhiteSpace(ProductPriceText)
            && !string.IsNullOrWhiteSpace(ProductQuantityText)
            && !string.IsNullOrWhiteSpace(_selectedImageUrl);
    }

    private void RefreshSubmitAvailability()
    {
        OnPropertyChanged(nameof(IsNameMissing));
        OnPropertyChanged(nameof(IsShortDescriptionMissing));
        OnPropertyChanged(nameof(IsFullDescriptionMissing));
        OnPropertyChanged(nameof(IsCategoryMissing));
        OnPropertyChanged(nameof(IsPriceMissing));
        OnPropertyChanged(nameof(IsQuantityMissing));
        OnPropertyChanged(nameof(IsImageMissing));

        IsSubmitEnabled = CanSubmitProduct();
        (SubmitProductCommand as Command)?.ChangeCanExecute();
    }

    private async Task SubmitProductAsync()
    {
        if (_isSubmittingProduct)
        {
            return;
        }

        if (!ValidateProductInputs(out var price, out var quantity))
        {
            return;
        }

        try
        {
            _isSubmittingProduct = true;
            RefreshSubmitAvailability();
            CreationStatusColor = Colors.Gold;
            CreationStatusMessage = "Création du produit en cours";

            var request = new ProductCreateRequest
            {
                Nom = NewProductName.Trim(),
                Description = NewProductFullDescription.Trim(),
                DescriptionCourte = NewProductShortDescription.Trim(),
                Categorie = SelectedCategory?.Name?.Trim() ?? string.Empty,
                Prix = price,
                Quantite = quantity,
                Image = _selectedImageUrl,
                Images = string.IsNullOrWhiteSpace(_selectedImageUrl)
                    ? new List<string>()
                    : new List<string> { _selectedImageUrl }
            };

            var success = await _apis.PostBoolAsync("/api/produits", request).ConfigureAwait(false);

            if (success)
            {
                CreationStatusColor = Colors.LimeGreen;
                CreationStatusMessage = "Produit créé avec succès.";
                ClearForm();
                await LoadProductsAsync(forceRefresh: true);
            }
            else
            {
                CreationStatusColor = Colors.OrangeRed;
                CreationStatusMessage = "La création du produit a échoué.";
            }
        }
        catch (HttpRequestException ex)
        {
            CreationStatusColor = Colors.OrangeRed;
            CreationStatusMessage = "Impossible de contacter le serveur pour créer le produit.";
            Debug.WriteLine($"[PRODUCTS] Erreur HTTP (create) : {ex}");
        }
        catch (Exception ex)
        {
            CreationStatusColor = Colors.OrangeRed;
            CreationStatusMessage = $"Erreur lors de la création : {ex.Message}";
            Debug.WriteLine($"[PRODUCTS] Erreur inattendue (create) : {ex}");
        }
        finally
        {
            _isSubmittingProduct = false;
            RefreshSubmitAvailability();
        }
    }

    private bool ValidateProductInputs(out double price, out int quantity)
    {
        price = 0;
        quantity = 0;

        if (string.IsNullOrWhiteSpace(NewProductName)
            || string.IsNullOrWhiteSpace(NewProductShortDescription)
            || string.IsNullOrWhiteSpace(NewProductFullDescription)
            || SelectedCategory is null)
        {
            CreationStatusColor = Colors.OrangeRed;
            CreationStatusMessage = "Merci de renseigner toutes les informations du produit.";
            return false;
        }

        if (!TryParseDouble(ProductPriceText, out price) || price <= 0)
        {
            CreationStatusColor = Colors.OrangeRed;
            CreationStatusMessage = "Indiquez un prix valide (ex : 12.5).";
            return false;
        }

        if (!int.TryParse(ProductQuantityText?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) || quantity < 0)
        {
            CreationStatusColor = Colors.OrangeRed;
            CreationStatusMessage = "Indiquez une quantité disponible valide.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_selectedImageUrl))
        {
            CreationStatusColor = Colors.OrangeRed;
            CreationStatusMessage = "Sélectionnez une image pour le produit.";
            return false;
        }

        return true;
    }

    private void ResetForm()
    {
        ClearForm();
        CreationStatusColor = Colors.Gold;
        CreationStatusMessage = DefaultCreationMessage;
    }

    private void ClearForm()
    {
        NewProductName = string.Empty;
        NewProductShortDescription = string.Empty;
        NewProductFullDescription = string.Empty;
        NewProductCategory = string.Empty;
        SelectedCategory = null;
        ProductPriceText = string.Empty;
        ProductQuantityText = string.Empty;
        SelectedLibraryImage = null;
        _selectedImageUrl = null;
        SelectedImageName = "Aucune image sélectionnée.";
        CategoryStatusMessage = AvailableCategories.Count == 0
            ? "Aucune catégorie disponible."
            : "Sélectionnez une catégorie pour ce produit.";
        RefreshSubmitAvailability();
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        var normalized = text?.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }
}
