using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Globalization;
using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Newtonsoft.Json;

namespace GDM2026.ViewModels;

public class PromoPageViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionLoaded;
    private bool _productsLoaded;
    private bool _categoriesLoaded;
    private bool _isCategoriesLoading;
    private bool _isCategoriesLoaded;
    private bool _isUpdateMode;
    private bool _isCreateMode;
    private bool _isFormSectionVisible;
    private bool _isPromosLoading;
    private bool _isFlashMode;
    private bool _isStandardModeVisible;

    private string _statusMessage = "Choisissez un mode pour commencer.";
    private string _formHeader = "Créer une promotion";
    private string _formHelperMessage = "Renseignez la période, le prix et les sélections avant de valider.";
    private string _promosStatusMessage = "Aucune promotion chargée.";
    private string _categoryPickerStatus = "Chargement des catégories...";
    private string _flashStatusMessage = string.Empty;
    private DateTime _dateDebutDate = DateTime.Today;
    private TimeSpan _dateDebutTime = TimeSpan.Zero;
    private DateTime _dateFinDate = DateTime.Today;
    private TimeSpan _dateFinTime = TimeSpan.Zero;
    private string _prixText = string.Empty;
    private string _productSearchText = string.Empty;
    private string _categorySearchText = string.Empty;

    // Champs pour le formulaire Flash
    private PromoProduct? _flashSelectedProduct;
    private string _flashPrix = string.Empty;
    private int _flashQuantite = 50;
    private DateTime _flashDateDebutDate = DateTime.Today;
    private TimeSpan _flashDateDebutTime = new TimeSpan(8, 0, 0);
    private DateTime _flashDateFinDate = DateTime.Today.AddDays(2);
    private TimeSpan _flashDateFinTime = new TimeSpan(18, 0, 0);
    private DateTime _flashDateDispoDate = DateTime.Today.AddDays(4);
    private TimeSpan _flashDateDispoTime = new TimeSpan(10, 0, 0);

    private PromoProduct? _selectedProduct;
    private PromoCategory? _selectedCategory;
    private PromoCategory? _selectedCategoryPicker;
    private Promo? _selectedPromo;

    private List<PromoProduct> _allProducts = new();
    private List<PromoCategory> _allCategories = new();

    public event EventHandler<string>? PromoSaved;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private sealed class PromoListResponse
    {
        [JsonProperty("data")]
        public List<Promo>? Data { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private sealed class PromoResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("data")]
        public Promo? Data { get; set; }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private sealed class PromoCategoryListResponse
    {
        [JsonProperty("data")]
        public List<PromoCategory>? Data { get; set; }
    }

    public PromoPageViewModel()
    {
        Promos = new ObservableCollection<Promo>();
        ProductResults = new ObservableCollection<PromoProduct>();
        CategoryResults = new ObservableCollection<PromoCategory>();
        AvailableCategories = new ObservableCollection<PromoCategory>();
        FlashProductList = new ObservableCollection<PromoProduct>();
        StandardProductList = new ObservableCollection<PromoProduct>();

        GoBackCommand = new Command(async () => await NavigateBackAsync());
        ShowCreatePanelCommand = new Command(ActivateCreateMode);
        ShowUpdatePanelCommand = new Command(ActivateUpdateMode);
        RefreshPromosCommand = new Command(async () => await LoadPromosAsync());
        SearchProductsCommand = new Command(async () => await SearchProductsAsync());
        SearchCategoriesCommand = new Command(async () => await SearchCategoriesAsync());
        CreatePromoCommand = new Command(async () => await CreatePromoAsync(), CanCreatePromo);
        UpdatePromoCommand = new Command(async () => await UpdatePromoAsync(), CanUpdatePromo);

        // Commandes Flash
        CreateFlashPromoCommand = new Command(async () => await CreateFlashPromoAsync(), CanCreateFlashPromo);
        IncrementQuantityCommand = new Command(() => FlashQuantite = Math.Min(FlashQuantite + 1, 999));
        DecrementQuantityCommand = new Command(() => FlashQuantite = Math.Max(FlashQuantite - 1, 1));
        SetQuantityCommand = new Command<string>(qty => { if (int.TryParse(qty, out var q)) FlashQuantite = q; });

        StatusMessage = "Choisissez un mode pour commencer.";
    }

    public ICommand GoBackCommand { get; }
    public ICommand ShowCreatePanelCommand { get; }
    public ICommand ShowUpdatePanelCommand { get; }
    public ObservableCollection<Promo> Promos { get; }
    public ObservableCollection<PromoProduct> ProductResults { get; }
    public ObservableCollection<PromoCategory> CategoryResults { get; }
    public ObservableCollection<PromoCategory> AvailableCategories { get; }
    public ObservableCollection<PromoProduct> FlashProductList { get; }
    public ObservableCollection<PromoProduct> StandardProductList { get; }

    public ICommand RefreshPromosCommand { get; }
    public ICommand SearchProductsCommand { get; }
    public ICommand SearchCategoriesCommand { get; }
    public ICommand CreatePromoCommand { get; }
    public ICommand UpdatePromoCommand { get; }

    // Commandes Flash
    public ICommand CreateFlashPromoCommand { get; }
    public ICommand IncrementQuantityCommand { get; }
    public ICommand DecrementQuantityCommand { get; }
    public ICommand SetQuantityCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string FormHeader
    {
        get => _formHeader;
        set => SetProperty(ref _formHeader, value);
    }

    public string FormHelperMessage
    {
        get => _formHelperMessage;
        set => SetProperty(ref _formHelperMessage, value);
    }

    public string PromosStatusMessage
    {
        get => _promosStatusMessage;
        set => SetProperty(ref _promosStatusMessage, value);
    }

    public bool IsUpdateMode
    {
        get => _isUpdateMode;
        set => SetProperty(ref _isUpdateMode, value);
    }

    public bool IsFormSectionVisible
    {
        get => _isFormSectionVisible;
        set => SetProperty(ref _isFormSectionVisible, value);
    }

    public bool IsPromosLoading
    {
        get => _isPromosLoading;
        set => SetProperty(ref _isPromosLoading, value);
    }

    public bool IsCategoriesLoading
    {
        get => _isCategoriesLoading;
        set => SetProperty(ref _isCategoriesLoading, value);
    }

    public bool IsCategoriesLoaded
    {
        get => _isCategoriesLoaded;
        set => SetProperty(ref _isCategoriesLoaded, value);
    }

    public bool IsFlashMode
    {
        get => _isFlashMode;
        set => SetProperty(ref _isFlashMode, value);
    }

    public bool IsStandardModeVisible
    {
        get => _isStandardModeVisible;
        set => SetProperty(ref _isStandardModeVisible, value);
    }

    public bool IsCreateMode
    {
        get => _isCreateMode;
        set => SetProperty(ref _isCreateMode, value);
    }

    public string SelectedProductPriceLabel => SelectedProduct is null
        ? string.Empty
        : $"Prix normal: {SelectedProduct.DisplayPrice}";

    public string CategoryPickerStatus
    {
        get => _categoryPickerStatus;
        set => SetProperty(ref _categoryPickerStatus, value);
    }

    public string FlashStatusMessage
    {
        get => _flashStatusMessage;
        set => SetProperty(ref _flashStatusMessage, value);
    }

    public PromoCategory? SelectedCategoryPicker
    {
        get => _selectedCategoryPicker;
        set
        {
            if (SetProperty(ref _selectedCategoryPicker, value))
            {
                OnCategoryPickerChanged(value);
            }
        }
    }

    // Propriétés Flash
    public PromoProduct? FlashSelectedProduct
    {
        get => _flashSelectedProduct;
        set
        {
            if (SetProperty(ref _flashSelectedProduct, value))
            {
                OnPropertyChanged(nameof(FlashSelectedProductLabel));
                RefreshCommands();
            }
        }
    }

    public string FlashSelectedProductLabel => FlashSelectedProduct is null
        ? string.Empty
        : $"Prix normal: {FlashSelectedProduct.DisplayPrice}";

    public string FlashPrix
    {
        get => _flashPrix;
        set
        {
            if (SetProperty(ref _flashPrix, value))
                RefreshCommands();
        }
    }

    public int FlashQuantite
    {
        get => _flashQuantite;
        set
        {
            if (SetProperty(ref _flashQuantite, value))
                RefreshCommands();
        }
    }

    public DateTime FlashDateDebutDate
    {
        get => _flashDateDebutDate;
        set => SetProperty(ref _flashDateDebutDate, value);
    }

    public TimeSpan FlashDateDebutTime
    {
        get => _flashDateDebutTime;
        set => SetProperty(ref _flashDateDebutTime, value);
    }

    public DateTime FlashDateFinDate
    {
        get => _flashDateFinDate;
        set => SetProperty(ref _flashDateFinDate, value);
    }

    public TimeSpan FlashDateFinTime
    {
        get => _flashDateFinTime;
        set => SetProperty(ref _flashDateFinTime, value);
    }

    public DateTime FlashDateDispoDate
    {
        get => _flashDateDispoDate;
        set => SetProperty(ref _flashDateDispoDate, value);
    }

    public TimeSpan FlashDateDispoTime
    {
        get => _flashDateDispoTime;
        set => SetProperty(ref _flashDateDispoTime, value);
    }

    public DateTime DateDebutDate
    {
        get => _dateDebutDate;
        set
        {
            if (SetProperty(ref _dateDebutDate, value))
                RefreshCommands();
        }
    }

    public TimeSpan DateDebutTime
    {
        get => _dateDebutTime;
        set
        {
            if (SetProperty(ref _dateDebutTime, value))
                RefreshCommands();
        }
    }

    public DateTime DateFinDate
    {
        get => _dateFinDate;
        set
        {
            if (SetProperty(ref _dateFinDate, value))
                RefreshCommands();
        }
    }

    public TimeSpan DateFinTime
    {
        get => _dateFinTime;
        set
        {
            if (SetProperty(ref _dateFinTime, value))
                RefreshCommands();
        }
    }

    public string PrixText
    {
        get => _prixText;
        set
        {
            if (SetProperty(ref _prixText, value))
                RefreshCommands();
        }
    }

    public string ProductSearchText
    {
        get => _productSearchText;
        set => SetProperty(ref _productSearchText, value);
    }

    public string CategorySearchText
    {
        get => _categorySearchText;
        set => SetProperty(ref _categorySearchText, value);
    }

    public PromoProduct? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                OnPropertyChanged(nameof(SelectedProductLabel));
                OnPropertyChanged(nameof(SelectedProductPriceLabel));
                RefreshCommands();
            }
        }
    }

    public PromoCategory? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                OnPropertyChanged(nameof(SelectedCategoryLabel));
                RefreshCommands();
            }
        }
    }

    public Promo? SelectedPromo
    {
        get => _selectedPromo;
        set
        {
            if (SetProperty(ref _selectedPromo, value))
            {
                ApplyPromoSelection(value);
                RefreshCommands();
            }
        }
    }

    public string SelectedProductLabel => SelectedProduct is null
        ? "Aucun produit sélectionné"
        : $"Produit #{SelectedProduct.Id} — {SelectedProduct.DisplayName}";

    public string SelectedCategoryLabel => SelectedCategory is null
        ? "Non sélectionnée"
        : SelectedCategory.DisplayName;

    public async Task InitializeAsync()
    {
        // Charger les catégories en premier (nécessaire pour le picker)
        if (!_categoriesLoaded && AvailableCategories.Count == 0)
        {
            await LoadCategoriesForPickerAsync();
        }

        // Puis charger les promos si nécessaire
        if (Promos.Count == 0)
        {
            await LoadPromosAsync();
        }
    }

    private async Task LoadCategoriesForPickerAsync()
    {
        try
        {
            IsCategoriesLoading = true;
            IsCategoriesLoaded = false;
            CategoryPickerStatus = "Chargement des catégories...";

            if (!await EnsureSessionAsync())
            {
                CategoryPickerStatus = "Session expirée.";
                return;
            }

            _allCategories = await FetchCategoriesAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AvailableCategories.Clear();
                foreach (var cat in _allCategories)
                    AvailableCategories.Add(cat);

                IsCategoriesLoaded = true;
                CategoryPickerStatus = _allCategories.Count == 0
                    ? "Aucune catégorie disponible."
                    : "Sélectionnez le type de promotion.";
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PROMO] category picker load error: {ex}");
            CategoryPickerStatus = "Erreur de chargement des catégories.";
        }
        finally
        {
            IsCategoriesLoading = false;
        }
    }

    private async void OnCategoryPickerChanged(PromoCategory? category)
    {
        // Reset des modes
        IsFlashMode = false;
        IsStandardModeVisible = false;
        IsFormSectionVisible = false;
        IsUpdateMode = false;

        if (category == null)
        {
            CategoryPickerStatus = "Sélectionnez le type de promotion.";
            return;
        }

        // Détection du mode flash (nom contient "flash" insensible à la casse)
        var isFlash = category.Name?.ToLowerInvariant().Contains("flash") ?? false;

        if (isFlash)
        {
            IsFlashMode = true;
            FlashStatusMessage = string.Empty;
            CategoryPickerStatus = $"Mode Flash activé - Catégorie: {category.DisplayName}";

            // Charger les produits pour le picker flash
            await LoadFlashProductsAsync();
        }
        else
        {
            IsStandardModeVisible = true;
            CategoryPickerStatus = $"Catégorie: {category.DisplayName} - Choisissez une action.";
            // Pré-sélectionner cette catégorie pour le formulaire standard
            SelectedCategory = category;
        }
    }

    private async Task LoadFlashProductsAsync()
    {
        try
        {
            IsBusy = true;
            FlashStatusMessage = "Chargement des produits...";

            if (!await EnsureSessionAsync())
                return;

            if (!_productsLoaded)
            {
                _allProducts = await FetchProductsAsync();
                _productsLoaded = true;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                FlashProductList.Clear();
                foreach (var product in _allProducts.Take(100))
                    FlashProductList.Add(product);

                FlashStatusMessage = FlashProductList.Count == 0
                    ? "Aucun produit disponible."
                    : string.Empty;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PROMO] flash products load error: {ex}");
            FlashStatusMessage = "Erreur chargement produits.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ActivateCreateMode()
    {
        IsUpdateMode = false;
        IsCreateMode = true;
        IsFormSectionVisible = true;
        FormHeader = "Nouvelle promotion";
        FormHelperMessage = "Sélectionnez un produit, définissez le prix et la période.";
        StatusMessage = string.Empty;
        SelectedPromo = null;
        SelectedProduct = null;
        PrixText = string.Empty;
        DateDebutDate = DateTime.Today;
        DateDebutTime = new TimeSpan(8, 0, 0);
        DateFinDate = DateTime.Today.AddDays(7);
        DateFinTime = new TimeSpan(23, 59, 0);

        await LoadStandardProductsAsync();
        RefreshCommands();
    }

    private async void ActivateUpdateMode()
    {
        IsUpdateMode = true;
        IsCreateMode = false;
        IsFormSectionVisible = true;
        FormHeader = "Modifier une promotion";
        FormHelperMessage = "Sélectionnez d'abord une promotion existante ci-dessus.";
        StatusMessage = string.Empty;

        if (Promos.Count == 0)
            await LoadPromosAsync();

        await LoadStandardProductsAsync();
        RefreshCommands();
    }

    private async Task LoadStandardProductsAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Chargement des produits...";

            if (!await EnsureSessionAsync())
                return;

            if (!_productsLoaded)
            {
                _allProducts = await FetchProductsAsync();
                _productsLoaded = true;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StandardProductList.Clear();
                foreach (var product in _allProducts.Take(100))
                    StandardProductList.Add(product);

                StatusMessage = string.Empty;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PROMO] standard products load error: {ex}");
            StatusMessage = "Erreur chargement produits.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task NavigateBackAsync()
    {
        if (Shell.Current == null)
            return Task.CompletedTask;

        return Shell.Current.GoToAsync("..", animate: false);
    }

    private async Task<bool> EnsureSessionAsync()
    {
        if (_sessionLoaded)
            return true;

        if (!await _sessionService.LoadAsync())
        {
            StatusMessage = "Session expirée, reconnectez-vous.";
            return false;
        }

        _apis.SetBearerToken(_sessionService.AuthToken);
        _sessionLoaded = true;
        return true;
    }

    private async Task LoadPromosAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            IsPromosLoading = true;
            StatusMessage = "Chargement des promotions…";
            RefreshCommands();
            
            if (!await EnsureSessionAsync())
                return;

            var response = await _apis.PostAsync<object, PromoListResponse>("/promo/list", new { });
            var data = response?.Data ?? new List<Promo>();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Promos.Clear();
                foreach (var promo in data.OrderByDescending(p => p.Id))
                    Promos.Add(promo);

                PromosStatusMessage = Promos.Count == 0
                    ? "Aucune promotion pour le moment."
                    : $"{Promos.Count} promotion(s) chargée(s).";
                StatusMessage = PromosStatusMessage;
            });
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[PROMO] http error: {ex.Message}");
            PromosStatusMessage = "Impossible de joindre l'API des promotions.";
            StatusMessage = PromosStatusMessage;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PROMO] unexpected error: {ex}");
            PromosStatusMessage = "Erreur inattendue lors du chargement.";
            StatusMessage = PromosStatusMessage;
        }
        finally
        {
            IsBusy = false;
            IsPromosLoading = false;
            RefreshCommands();
        }
    }

    private async Task SearchProductsAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Recherche des produits…";
            RefreshCommands();

            if (!await EnsureSessionAsync())
                return;

            if (!_productsLoaded)
            {
                _allProducts = await FetchProductsAsync();
                _productsLoaded = true;
            }

            var query = ProductSearchText?.Trim().ToLowerInvariant();
            var results = string.IsNullOrWhiteSpace(query)
                ? _allProducts
                : _allProducts.Where(p =>
                    p.DisplayName.ToLowerInvariant().Contains(query) ||
                    (p.DisplayDescription?.ToLowerInvariant().Contains(query) ?? false))
                    .ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ProductResults.Clear();
                foreach (var product in results.Take(50))
                    ProductResults.Add(product);

                StatusMessage = ProductResults.Count == 0
                    ? "Aucun produit trouvé."
                    : $"{ProductResults.Count} produit(s) affiché(s).";
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PROMO] product search error: {ex}");
            StatusMessage = "Erreur lors de la recherche des produits.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task SearchCategoriesAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            StatusMessage = "Recherche des catégories promo…";
            RefreshCommands();

            if (!await EnsureSessionAsync())
                return;

            if (!_categoriesLoaded)
            {
                _allCategories = await FetchCategoriesAsync();
                _categoriesLoaded = true;
            }

            var query = CategorySearchText?.Trim().ToLowerInvariant();
            var results = string.IsNullOrWhiteSpace(query)
                ? _allCategories
                : _allCategories.Where(c => c.DisplayName.ToLowerInvariant().Contains(query)).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CategoryResults.Clear();
                foreach (var category in results.Take(50))
                    CategoryResults.Add(category);

                StatusMessage = CategoryResults.Count == 0
                    ? "Aucune catégorie trouvée."
                    : $"{CategoryResults.Count} catégorie(s) affichée(s).";
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PROMO] category search error: {ex}");
            StatusMessage = "Erreur lors du chargement des catégories.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task<List<PromoProduct>> FetchProductsAsync()
    {
        var results = new List<PromoProduct>();
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
                var items = await _apis.GetListAsync<ProductCatalogItem>(endpoint).ConfigureAwait(false);
                if (items.Count > 0)
                {
                    return items.Select(MapProduct).ToList();
                }

                if (results.Count == 0)
                {
                    results = items.Select(MapProduct).ToList();
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                Debug.WriteLine($"[PROMO] product endpoint failed '{endpoint}': {ex.Message}");
            }
        }

        if (results.Count > 0)
            return results;

        if (lastError != null)
            throw lastError;

        return new List<PromoProduct>();
    }

    private static PromoProduct MapProduct(ProductCatalogItem item) => new()
    {
        Id = item.Id,
        NomProduit = item.DisplayName,
        Prix = item.Prix > 0 ? item.Prix : item.PrixPromo,
        Description = item.DescriptionLongue,
        DescriptionCourte = item.DescriptionCourte
    };

    private async Task<List<PromoCategory>> FetchCategoriesAsync()
    {
        var response = await _apis.PostAsync<object, PromoCategoryListResponse>("/categorie/promo/list", new { });
        return response?.Data ?? new List<PromoCategory>();
    }

    private async Task CreatePromoAsync()
    {
        if (IsBusy)
            return;

        if (!await EnsureSessionAsync())
            return;

        if (!TryParseForm(out var debut, out var fin, out var prix))
            return;

        if (SelectedProduct is null || SelectedCategory is null)
        {
            StatusMessage = "Choisissez un produit et une catégorie.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Création de la promotion…";
            RefreshCommands();

            var payload = new
            {
                dateDebut = debut.ToString("o"),
                dateFin = fin.ToString("o"),
                prix,
                leProduitId = SelectedProduct.Id,
                laCategoriePromoId = SelectedCategory.Id
            };

            var response = await _apis.PostAsync<object, PromoResponse>("/promo/create", payload);
            if (response?.Data != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Promos.Insert(0, response.Data);
                    SelectedPromo = response.Data;
                });

                NotifyPromoSaved(response?.Message ?? "Promotion créée avec succès.");
            }

            StatusMessage = response?.Message ?? "Promotion créée avec succès.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PROMO] create error: {ex}");
            StatusMessage = "Erreur lors de la création de la promotion.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task UpdatePromoAsync()
    {
        if (IsBusy)
            return;

        if (SelectedPromo is null)
        {
            StatusMessage = "Sélectionnez une promotion à mettre à jour.";
            return;
        }

        if (!await EnsureSessionAsync())
            return;

        if (!TryParseForm(out var debut, out var fin, out var prix))
            return;

        if (SelectedProduct is null || SelectedCategory is null)
        {
            StatusMessage = "Choisissez un produit et une catégorie.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Mise à jour de la promotion…";
            RefreshCommands();

            var payload = new
            {
                id = SelectedPromo.Id,
                dateDebut = debut.ToString("o"),
                dateFin = fin.ToString("o"),
                prix,
                leProduitId = SelectedProduct.Id,
                laCategoriePromoId = SelectedCategory.Id
            };

            var response = await _apis.PostAsync<object, PromoResponse>("/promo/update", payload);
            if (response?.Data != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var existing = Promos.FirstOrDefault(p => p.Id == response.Data.Id);
                    if (existing != null)
                    {
                        var index = Promos.IndexOf(existing);
                        Promos[index] = response.Data;
                    }
                    else
                    {
                        Promos.Insert(0, response.Data);
                    }

                    SelectedPromo = response.Data;
                });

                NotifyPromoSaved(response?.Message ?? "Promotion mise à jour.");
            }

            StatusMessage = response?.Message ?? "Promotion mise à jour.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PROMO] update error: {ex}");
            StatusMessage = "Erreur lors de la mise à jour.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task CreateFlashPromoAsync()
    {
        if (IsBusy)
            return;

        if (!await EnsureSessionAsync())
        {
            FlashStatusMessage = "Session expirée, reconnectez-vous.";
            return;
        }

        if (FlashSelectedProduct is null)
        {
            FlashStatusMessage = "Sélectionnez un produit.";
            return;
        }

        if (SelectedCategoryPicker is null)
        {
            FlashStatusMessage = "Catégorie non sélectionnée.";
            return;
        }

        if (!TryParsePrice(FlashPrix, out var prix) || prix <= 0)
        {
            FlashStatusMessage = "Prix invalide.";
            return;
        }

        var dateDebut = DateTime.SpecifyKind(FlashDateDebutDate.Date + FlashDateDebutTime, DateTimeKind.Local);
        var dateFin = DateTime.SpecifyKind(FlashDateFinDate.Date + FlashDateFinTime, DateTimeKind.Local);
        var dateDispo = DateTime.SpecifyKind(FlashDateDispoDate.Date + FlashDateDispoTime, DateTimeKind.Local);

        if (dateFin <= dateDebut)
        {
            FlashStatusMessage = "La fin doit être après le début.";
            return;
        }

        try
        {
            IsBusy = true;
            FlashStatusMessage = "Création de la promo flash...";
            RefreshCommands();

            var payload = new
            {
                produitId = FlashSelectedProduct.Id,
                categoriePromoId = SelectedCategoryPicker.Id,
                dateDebut = dateDebut.ToString("yyyy-MM-dd HH:mm:ss"),
                dateFin = dateFin.ToString("yyyy-MM-dd HH:mm:ss"),
                prix,
                quantite = FlashQuantite,
                dateDisponibilite = dateDispo.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var response = await _apis.PostAsync<object, FlashPromoResponse>("/api/mobile/AjoutPromo", payload);

            if (response?.Success == true || response?.Data != null)
            {
                FlashStatusMessage = response?.Message ?? "Promo flash créée avec succès!";
                NotifyPromoSaved(FlashStatusMessage);

                // Reset du formulaire
                FlashSelectedProduct = null;
                FlashPrix = string.Empty;
                FlashQuantite = 50;
            }
            else
            {
                FlashStatusMessage = response?.Message ?? "Erreur lors de la création.";
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[PROMO FLASH] http error: {ex.Message}");
            FlashStatusMessage = "Impossible de joindre le serveur.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PROMO FLASH] error: {ex}");
            FlashStatusMessage = "Erreur inattendue.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private bool CanCreateFlashPromo()
    {
        return !IsBusy
            && FlashSelectedProduct != null
            && !string.IsNullOrWhiteSpace(FlashPrix)
            && FlashQuantite > 0;
    }

    private sealed class FlashPromoResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("data")]
        public object? Data { get; set; }
    }

    private bool TryParseForm(out DateTime debut, out DateTime fin, out double prix)
    {
        debut = default;
        fin = default;
        prix = default;

        debut = DateTime.SpecifyKind(DateDebutDate.Date + DateDebutTime, DateTimeKind.Local);
        fin = DateTime.SpecifyKind(DateFinDate.Date + DateFinTime, DateTimeKind.Local);

        if (!TryParsePrice(PrixText, out prix))
        {
            StatusMessage = "Prix invalide.";
            return false;
        }

        if (fin < debut)
        {
            StatusMessage = "La date de fin doit être postérieure à la date de début.";
            return false;
        }

        return true;
    }

    private static bool TryParsePrice(string? raw, out double price)
    {
        price = default;

        var input = raw?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var styles = NumberStyles.Number;
        if (double.TryParse(input, styles, CultureInfo.CurrentCulture, out price))
        {
            return true;
        }

        if (double.TryParse(input, styles, CultureInfo.InvariantCulture, out price))
        {
            return true;
        }

        var normalized = input.Replace(',', '.');
        return double.TryParse(normalized, styles, CultureInfo.InvariantCulture, out price);
    }

    private void ApplyPromoSelection(Promo? promo)
    {
        if (promo is null)
        {
            DateDebutDate = DateTime.Today;
            DateFinDate = DateTime.Today;
            DateDebutTime = TimeSpan.Zero;
            DateFinTime = TimeSpan.Zero;
            PrixText = string.Empty;
            SelectedProduct = null;
            SelectedCategory = null;
            return;
        }

        DateDebutDate = promo.DateDebut?.Date ?? DateTime.Today;
        DateFinDate = promo.DateFin?.Date ?? DateTime.Today;
        DateDebutTime = promo.DateDebut?.TimeOfDay ?? TimeSpan.Zero;
        DateFinTime = promo.DateFin?.TimeOfDay ?? TimeSpan.Zero;
        PrixText = promo.Prix.ToString("0.##");
        SelectedProduct = promo.LeProduit;
        SelectedCategory = promo.LaCategoriePromo;
    }

    private bool CanCreatePromo()
    {
        return !IsBusy
            && SelectedProduct != null
            && SelectedCategory != null
            && !string.IsNullOrWhiteSpace(PrixText);
    }

    private bool CanUpdatePromo()
    {
        return SelectedPromo != null && CanCreatePromo();
    }

    private void RefreshCommands()
    {
        (CreatePromoCommand as Command)?.ChangeCanExecute();
        (UpdatePromoCommand as Command)?.ChangeCanExecute();
        (CreateFlashPromoCommand as Command)?.ChangeCanExecute();
    }

    private void NotifyPromoSaved(string message)
    {
        MainThread.BeginInvokeOnMainThread(() => PromoSaved?.Invoke(this, message));
    }
}
