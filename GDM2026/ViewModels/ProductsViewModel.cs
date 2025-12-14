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
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class ProductsViewModel : BaseViewModel
{
    private const bool ProductLoadingEnabled = false;
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionPrepared;
    private bool _hasLoaded;
    private bool _isRefreshing;
    private string _statusMessage = "Chargement du catalogue…";
    private string _searchText = string.Empty;

    private bool _isFormVisible;
    private string _newProductName = string.Empty;
    private string _newProductShortDescription = string.Empty;
    private string _newProductFullDescription = string.Empty;
    private string _newProductCategory = string.Empty;
    private string _productPriceText = string.Empty;
    private string _productQuantityText = string.Empty;
    private string _creationStatusMessage = "Remplissez le formulaire pour créer un produit.";
    private Color _creationStatusColor = Colors.Gold;
    private bool _isSubmittingProduct;

    private bool _imageLibraryLoaded;
    private bool _isImageLibraryLoading;
    private string _imageLibraryMessage = "Sélectionnez une image depuis la bibliothèque.";
    private string _imageSearchTerm = string.Empty;
    private IList<object>? _selectedLibraryImages;
    private string _selectedImageName = "Aucune image sélectionnée.";
    private List<string> _selectedImageUrls = new();

    private bool _categoriesLoaded;
    private bool _isCategoriesLoading;
    private string _categoryStatusMessage = "Choisissez une catégorie pour le produit.";
    private SubCategory? _selectedCategory;

    public ProductsViewModel()
    {
        Products = new ObservableCollection<ProductCatalogItem>();
        FilteredProducts = new ObservableCollection<ProductCatalogItem>();
        ImageLibrary = new ObservableCollection<AdminImage>();
        FilteredImageLibrary = new ObservableCollection<AdminImage>();
        SelectedImages = new ObservableCollection<AdminImage>();
        Categories = new ObservableCollection<SubCategory>();

        RefreshCommand = new Command(async () => await LoadProductsAsync(forceRefresh: true));
        ToggleFormCommand = new Command(async () => await ToggleFormAsync());
        SubmitProductCommand = new Command(async () => await SubmitProductAsync(), CanSubmitProduct);
    }

    public ObservableCollection<ProductCatalogItem> Products { get; }

    public ObservableCollection<ProductCatalogItem> FilteredProducts { get; }

    public ObservableCollection<AdminImage> ImageLibrary { get; }

    public ObservableCollection<AdminImage> FilteredImageLibrary { get; }

    public ObservableCollection<AdminImage> SelectedImages { get; }

    public ObservableCollection<SubCategory> Categories { get; }

    public ICommand RefreshCommand { get; }

    public ICommand ToggleFormCommand { get; }

    public ICommand SubmitProductCommand { get; }

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
                ApplyFilter();
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
                _ = EnsureCategoriesLoadedAsync();
            }
        }
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

    public IList<object>? SelectedLibraryImages
    {
        get => _selectedLibraryImages;
        set
        {
            if (SetProperty(ref _selectedLibraryImages, value))
            {
                var items = value?.OfType<AdminImage>().ToList() ?? new List<AdminImage>();
                ApplyImageSelection(items);
            }
        }
    }

    public string SelectedImageName
    {
        get => _selectedImageName;
        set => SetProperty(ref _selectedImageName, value);
    }

    public bool HasSelectedImages => SelectedImages.Any();

    public bool IsCategoriesLoading
    {
        get => _isCategoriesLoading;
        set => SetProperty(ref _isCategoriesLoading, value);
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
                NewProductCategory = value?.DisplayName ?? string.Empty;
                CategoryStatusMessage = value is null
                    ? "Choisissez une catégorie pour le produit."
                    : $"Catégorie sélectionnée : {value.DisplayName}";
                RefreshSubmitAvailability();
            }
        }
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

        if (!_categoriesLoaded)
        {
            await EnsureCategoriesLoadedAsync();
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
            StatusMessage = forceRefresh ? "Actualisation du catalogue…" : "Chargement du catalogue…";

            var items = await FetchProductsAsync().ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Products.Clear();
                foreach (var item in items)
                {
                    Products.Add(item);
                }

                _hasLoaded = true;
                ApplyFilter();

                if (!Products.Any())
                {
                    StatusMessage = "Aucun produit à afficher pour le moment.";
                }
                else if (string.IsNullOrWhiteSpace(SearchText))
                {
                    StatusMessage = $"{Products.Count} produit(s) chargé(s).";
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

    private void ApplyFilter()
    {
        IEnumerable<ProductCatalogItem> source = Products;
        var query = SearchText?.Trim();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.ToLowerInvariant();
            source = source.Where(p =>
                (p.DisplayName?.ToLowerInvariant().Contains(normalized) ?? false) ||
                (p.Description?.ToLowerInvariant().Contains(normalized) ?? false) ||
                (p.Categorie?.ToLowerInvariant().Contains(normalized) ?? false));
        }

        FilteredProducts.Clear();
        foreach (var item in source)
        {
            FilteredProducts.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            StatusMessage = FilteredProducts.Count == 0
                ? "Aucun produit ne correspond à cette recherche."
                : $"{FilteredProducts.Count} résultat(s) pour \"{query}\".";
        }
        else if (Products.Count > 0)
        {
            StatusMessage = $"{Products.Count} produit(s) chargé(s).";
        }
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

    private async Task EnsureImageLibraryLoadedAsync()
    {
        if (_imageLibraryLoaded || IsImageLibraryLoading)
        {
            return;
        }

        await LoadImageLibraryAsync();
    }

    private async Task EnsureCategoriesLoadedAsync()
    {
        if (_categoriesLoaded || IsCategoriesLoading)
        {
            return;
        }

        await LoadCategoriesAsync();
    }

    private async Task LoadImageLibraryAsync()
    {
        try
        {
            IsImageLibraryLoading = true;
            ImageLibraryMessage = "Chargement de la bibliothèque d'images…";

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

    private async Task LoadCategoriesAsync()
    {
        try
        {
            IsCategoriesLoading = true;
            CategoryStatusMessage = "Chargement des catégories…";

            var categories = await _apis.GetListAsync<SubCategory>("/api/crud/categorie/list").ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Categories.Clear();
                foreach (var category in categories)
                {
                    Categories.Add(category);
                }

                _categoriesLoaded = true;
                CategoryStatusMessage = Categories.Count == 0
                    ? "Aucune catégorie disponible."
                    : "Sélectionnez une catégorie pour ce produit.";
            });
        }
        catch (HttpRequestException ex)
        {
            CategoryStatusMessage = "Impossible de charger les catégories.";
            Debug.WriteLine($"[PRODUCTS] Erreur HTTP (categories) : {ex}");
        }
        catch (Exception ex)
        {
            CategoryStatusMessage = "Erreur lors du chargement des catégories.";
            Debug.WriteLine($"[PRODUCTS] Erreur inattendue (categories) : {ex}");
        }
        finally
        {
            IsCategoriesLoading = false;
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

    private void ApplyImageSelection(List<AdminImage> images)
    {
        _selectedImageUrls = images
            .Select(img => img.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct()
            .Select(url => url!)
            .ToList();

        SelectedImages.Clear();
        foreach (var image in images)
        {
            SelectedImages.Add(image);
        }
        OnPropertyChanged(nameof(HasSelectedImages));

        if (_selectedImageUrls.Count == 0)
        {
            SelectedImageName = "Aucune image sélectionnée.";
        }
        else if (_selectedImageUrls.Count == 1)
        {
            SelectedImageName = "1 image sélectionnée.";
        }
        else
        {
            SelectedImageName = $"{_selectedImageUrls.Count} images sélectionnées.";
        }

        CreationStatusColor = Colors.Gold;
        CreationStatusMessage = _selectedImageUrls.Count == 0
            ? "Sélectionnez au moins une image."
            : "Image(s) prête(s) pour la création.";
        RefreshSubmitAvailability();
    }

    private bool CanSubmitProduct()
    {
        return !_isSubmittingProduct
            && !string.IsNullOrWhiteSpace(NewProductName)
            && !string.IsNullOrWhiteSpace(NewProductShortDescription)
            && !string.IsNullOrWhiteSpace(NewProductFullDescription)
            && !string.IsNullOrWhiteSpace(NewProductCategory)
            && !string.IsNullOrWhiteSpace(ProductPriceText)
            && !string.IsNullOrWhiteSpace(ProductQuantityText)
            && _selectedImageUrls.Any();
    }

    private void RefreshSubmitAvailability()
    {
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
            CreationStatusMessage = "Création du produit en cours…";

            var request = new ProductCreateRequest
            {
                Nom = NewProductName.Trim(),
                Description = NewProductFullDescription.Trim(),
                DescriptionCourte = NewProductShortDescription.Trim(),
                Categorie = NewProductCategory.Trim(),
                Prix = price,
                Quantite = quantity,
                Image = _selectedImageUrls.FirstOrDefault(),
                Images = _selectedImageUrls.ToList()
            };

            var success = await _apis.PostBoolAsync("/api/crud/produit/create", request).ConfigureAwait(false);

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
            || string.IsNullOrWhiteSpace(NewProductCategory))
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

        if (!_selectedImageUrls.Any())
        {
            CreationStatusColor = Colors.OrangeRed;
            CreationStatusMessage = "Sélectionnez une image pour le produit.";
            return false;
        }

        return true;
    }

    private void ClearForm()
    {
        NewProductName = string.Empty;
        NewProductShortDescription = string.Empty;
        NewProductFullDescription = string.Empty;
        NewProductCategory = string.Empty;
        ProductPriceText = string.Empty;
        ProductQuantityText = string.Empty;
        SelectedCategory = null;
        SelectedLibraryImages = null;
        _selectedImageUrls.Clear();
        SelectedImages.Clear();
        OnPropertyChanged(nameof(HasSelectedImages));
        SelectedImageName = "Aucune image sélectionnée.";
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        var normalized = text?.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }
}
