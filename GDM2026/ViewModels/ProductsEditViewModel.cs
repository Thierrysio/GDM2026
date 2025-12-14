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

public class ProductsEditViewModel : BaseViewModel
{
    private const int PageSize = 10;

    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionLoaded;
    private bool _isSearching;
    private bool _hasMore = true;
    private string _searchText = string.Empty;
    private string _statusMessage = "Entrez un nom pour rechercher ou faites défiler pour charger plus.";

    private int _currentPage = 0;

    private ProductCatalogItem? _selectedProduct;
    private string _editName = string.Empty;
    private string _editShortDescription = string.Empty;
    private string _editFullDescription = string.Empty;
    private string _editCategory = string.Empty;
    private string _editPriceText = string.Empty;
    private string _editQuantityText = string.Empty;
    private string _selectedImagesSummary = "Aucune image sélectionnée.";
    private string _editStatusMessage = "Sélectionnez un produit à modifier.";
    private Color _editStatusColor = Colors.Gold;
    private bool _isUpdatingProduct;

    private bool _imageLibraryLoaded;
    private bool _isImageLibraryLoading;
    private string _imageLibraryMessage = "Sélectionnez des images dans la bibliothèque.";
    private string _imageSearchTerm = string.Empty;
    private IList<object>? _selectedLibraryImages;

    public ProductsEditViewModel()
    {
        VisibleProducts = new ObservableCollection<ProductCatalogItem>();
        ImageLibrary = new ObservableCollection<AdminImage>();
        FilteredImageLibrary = new ObservableCollection<AdminImage>();
        EditableImageUrls = new ObservableCollection<string>();

        SearchProductsCommand = new Command(async () => await SearchAsync());
        LoadMoreCommand = new Command(async () => await LoadNextPageAsync());
        SaveProductCommand = new Command(async () => await SaveSelectedProductAsync(), CanSaveProduct);
        RemoveImageCommand = new Command<string>(RemoveImage);
    }

    public ObservableCollection<ProductCatalogItem> VisibleProducts { get; }

    public ObservableCollection<AdminImage> ImageLibrary { get; }

    public ObservableCollection<AdminImage> FilteredImageLibrary { get; }

    public ObservableCollection<string> EditableImageUrls { get; }

    public ICommand SearchProductsCommand { get; }

    public ICommand LoadMoreCommand { get; }

    public ICommand SaveProductCommand { get; }

    public ICommand RemoveImageCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = SearchAsync(auto: true);
            }
        }
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
                LoadProductForEdit(value);
                OnPropertyChanged(nameof(IsEditFormVisible));
                RefreshSaveAvailability();
            }
        }
    }

    public bool IsEditFormVisible => SelectedProduct is not null;

    public string EditName
    {
        get => _editName;
        set
        {
            if (SetProperty(ref _editName, value))
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

    public string EditQuantityText
    {
        get => _editQuantityText;
        set
        {
            if (SetProperty(ref _editQuantityText, value))
            {
                RefreshSaveAvailability();
            }
        }
    }

    public string SelectedImagesSummary
    {
        get => _selectedImagesSummary;
        set => SetProperty(ref _selectedImagesSummary, value);
    }

    public bool HasEditableImages => EditableImageUrls.Any();

    public string EditStatusMessage
    {
        get => _editStatusMessage;
        set => SetProperty(ref _editStatusMessage, value);
    }

    public Color EditStatusColor
    {
        get => _editStatusColor;
        set => SetProperty(ref _editStatusColor, value);
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
                var selected = value?.OfType<AdminImage>().ToList() ?? new List<AdminImage>();
                ApplyImageSelection(selected);
            }
        }
    }

    public async Task InitializeAsync()
    {
        if (!_sessionLoaded)
        {
            await _sessionService.LoadAsync().ConfigureAwait(false);
            _apis.SetBearerToken(_sessionService.AuthToken);
            _sessionLoaded = true;
        }

        if (VisibleProducts.Count == 0)
        {
            await LoadNextPageAsync();
        }
    }

    private async Task SearchAsync(bool auto = false)
    {
        if (_isSearching)
        {
            return;
        }

        if (auto && string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        _isSearching = true;

        try
        {
            StatusMessage = string.IsNullOrWhiteSpace(SearchText)
                ? "Chargement des produits"
                : $"Recherche pour '{SearchText}'";

            VisibleProducts.Clear();
            _currentPage = 0;
            HasMore = true;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                await LoadNextPageAsync();
                return;
            }

            var products = await _apis.GetListAsync<ProductCatalogItem>($"/api/mobile/produits?search={Uri.EscapeDataString(SearchText)}")
                .ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var item in products)
                {
                    VisibleProducts.Add(item);
                }

                HasMore = false;
                StatusMessage = products.Count == 0
                    ? "Aucun produit trouvé."
                    : $"{products.Count} résultat(s).";
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

    private async Task LoadNextPageAsync()
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

            var skip = _currentPage * PageSize;
            var products = await FetchPagedProductsAsync(skip, PageSize).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var item in products)
                {
                    VisibleProducts.Add(item);
                }

                if (products.Count < PageSize)
                {
                    HasMore = false;
                }
                else
                {
                    _currentPage++;
                }

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

    private async Task<List<ProductCatalogItem>> FetchPagedProductsAsync(int skip, int take)
    {
        var endpoint = $"/api/mobile/produits?skip={skip}&take={take}";
        try
        {
            var data = await _apis.GetListAsync<ProductCatalogItem>(endpoint).ConfigureAwait(false);
            return data;
        }
        catch (Exception)
        {
            var fallback = await _apis.GetListAsync<ProductCatalogItem>("/api/mobile/GetListProduit").ConfigureAwait(false);
            return fallback.Skip(skip).Take(take).ToList();
        }
    }

    private void LoadProductForEdit(ProductCatalogItem? product)
    {
        if (product is null)
        {
            EditName = string.Empty;
            EditShortDescription = string.Empty;
            EditFullDescription = string.Empty;
            EditCategory = string.Empty;
            EditPriceText = string.Empty;
            EditQuantityText = string.Empty;
            EditableImageUrls.Clear();
            UpdateImageSummary();
            EditStatusMessage = "Sélectionnez un produit à modifier.";
            EditStatusColor = Colors.Gold;
            return;
        }

        EditName = product.Nom ?? string.Empty;
        EditShortDescription = product.DescriptionCourte ?? string.Empty;
        EditFullDescription = product.DescriptionLongue ?? string.Empty;
        EditCategory = product.Categorie ?? string.Empty;

        var price = product.Prix > 0 ? product.Prix : product.PrixPromo;
        EditPriceText = price > 0 ? price.ToString(CultureInfo.InvariantCulture) : string.Empty;
        EditQuantityText = product.Stock?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        EditableImageUrls.Clear();
        SelectedLibraryImages = null;
        var urls = new List<string?> { product.ImageUrl };
        if (product.Images is { Count: > 0 })
        {
            urls.AddRange(product.Images);
        }

        foreach (var url in urls.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u!.Trim()).Distinct())
        {
            EditableImageUrls.Add(url);
        }

        UpdateImageSummary();
        EditStatusMessage = "Produit prêt à être modifié.";
        EditStatusColor = Colors.Gold;
        _ = EnsureImageLibraryLoadedAsync();
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

    private void ApplyImageSelection(List<AdminImage> images)
    {
        var urls = images
            .Select(img => img.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!.Trim())
            .Distinct()
            .ToList();

        var added = false;
        foreach (var url in urls)
        {
            if (!EditableImageUrls.Contains(url))
            {
                EditableImageUrls.Add(url);
                added = true;
            }
        }

        if (added)
        {
            EditStatusColor = Colors.Gold;
            EditStatusMessage = "Image(s) ajoutée(s) à la fiche produit.";
        }

        UpdateImageSummary();
    }

    private async Task SaveSelectedProductAsync()
    {
        if (_isUpdatingProduct || SelectedProduct is null)
        {
            return;
        }

        if (!ValidateEditInputs(out var price, out var quantity))
        {
            return;
        }

        try
        {
            _isUpdatingProduct = true;
            RefreshSaveAvailability();
            EditStatusColor = Colors.Gold;
            EditStatusMessage = "Mise à jour du produit en cours…";

            var payload = new ProductUpdateRequest
            {
                Id = SelectedProduct.Id,
                Nom = EditName.Trim(),
                Description = EditFullDescription.Trim(),
                DescriptionCourte = EditShortDescription.Trim(),
                Categorie = EditCategory.Trim(),
                Prix = price,
                Quantite = quantity,
                Image = EditableImageUrls.FirstOrDefault(),
                Images = EditableImageUrls.ToList()
            };

            var updated = await _apis.PostBoolAsync("/api/crud/produit/update", payload).ConfigureAwait(false);

            if (updated)
            {
                EditStatusColor = Colors.LimeGreen;
                EditStatusMessage = "Produit mis à jour avec succès.";
                await SearchAsync(auto: true);
            }
            else
            {
                EditStatusColor = Colors.OrangeRed;
                EditStatusMessage = "La mise à jour du produit a échoué.";
            }
        }
        catch (HttpRequestException ex)
        {
            EditStatusColor = Colors.OrangeRed;
            EditStatusMessage = "Impossible de contacter le serveur pour mettre à jour.";
            Debug.WriteLine($"[PRODUCTS_EDIT] HTTP error (update): {ex}");
        }
        catch (Exception ex)
        {
            EditStatusColor = Colors.OrangeRed;
            EditStatusMessage = $"Erreur lors de la mise à jour : {ex.Message}";
            Debug.WriteLine($"[PRODUCTS_EDIT] unexpected error (update): {ex}");
        }
        finally
        {
            _isUpdatingProduct = false;
            RefreshSaveAvailability();
        }
    }

    private bool ValidateEditInputs(out double price, out int quantity)
    {
        price = 0;
        quantity = 0;

        if (SelectedProduct is null)
        {
            EditStatusColor = Colors.OrangeRed;
            EditStatusMessage = "Choisissez un produit avant de sauvegarder.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(EditName)
            || string.IsNullOrWhiteSpace(EditShortDescription)
            || string.IsNullOrWhiteSpace(EditFullDescription)
            || string.IsNullOrWhiteSpace(EditCategory))
        {
            EditStatusColor = Colors.OrangeRed;
            EditStatusMessage = "Merci de renseigner toutes les informations du produit.";
            return false;
        }

        if (!TryParseDouble(EditPriceText, out price) || price <= 0)
        {
            EditStatusColor = Colors.OrangeRed;
            EditStatusMessage = "Indiquez un prix valide (ex : 12.5).";
            return false;
        }

        if (!int.TryParse(EditQuantityText?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) || quantity < 0)
        {
            EditStatusColor = Colors.OrangeRed;
            EditStatusMessage = "Indiquez une quantité disponible valide.";
            return false;
        }

        if (!EditableImageUrls.Any())
        {
            EditStatusColor = Colors.OrangeRed;
            EditStatusMessage = "Ajoutez au moins une image au produit.";
            return false;
        }

        return true;
    }

    private bool CanSaveProduct()
    {
        return !_isUpdatingProduct
            && SelectedProduct is not null
            && !string.IsNullOrWhiteSpace(EditName)
            && !string.IsNullOrWhiteSpace(EditShortDescription)
            && !string.IsNullOrWhiteSpace(EditFullDescription)
            && !string.IsNullOrWhiteSpace(EditCategory)
            && !string.IsNullOrWhiteSpace(EditPriceText)
            && !string.IsNullOrWhiteSpace(EditQuantityText)
            && EditableImageUrls.Any();
    }

    private void RefreshSaveAvailability()
    {
        (SaveProductCommand as Command)?.ChangeCanExecute();
    }

    private void RemoveImage(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (EditableImageUrls.Remove(url))
        {
            UpdateImageSummary();
            EditStatusColor = Colors.Gold;
            EditStatusMessage = "Image retirée du produit.";
            RefreshSaveAvailability();
        }
    }

    private void UpdateImageSummary()
    {
        if (EditableImageUrls.Count == 0)
        {
            SelectedImagesSummary = "Aucune image sélectionnée.";
        }
        else if (EditableImageUrls.Count == 1)
        {
            SelectedImagesSummary = "1 image sélectionnée.";
        }
        else
        {
            SelectedImagesSummary = $"{EditableImageUrls.Count} images sélectionnées.";
        }

        OnPropertyChanged(nameof(HasEditableImages));
        RefreshSaveAvailability();
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        var normalized = text?.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }
}
