using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
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

    private string _statusMessage = "Chargez les promos pour commencer.";
    private string _dateDebutText = string.Empty;
    private string _dateFinText = string.Empty;
    private string _prixText = string.Empty;
    private string _productSearchText = string.Empty;
    private string _categorySearchText = string.Empty;

    private PromoProduct? _selectedProduct;
    private PromoCategory? _selectedCategory;
    private Promo? _selectedPromo;

    private List<PromoProduct> _allProducts = new();
    private List<PromoCategory> _allCategories = new();

    private sealed class PromoListResponse
    {
        [JsonProperty("data")]
        public List<Promo>? Data { get; set; }
    }

    private sealed class PromoResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("data")]
        public Promo? Data { get; set; }
    }

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

        GoBackCommand = new Command(async () => await NavigateBackAsync());
        RefreshPromosCommand = new Command(async () => await LoadPromosAsync());
        SearchProductsCommand = new Command(async () => await SearchProductsAsync());
        SearchCategoriesCommand = new Command(async () => await SearchCategoriesAsync());
        CreatePromoCommand = new Command(async () => await CreatePromoAsync(), CanCreatePromo);
        UpdatePromoCommand = new Command(async () => await UpdatePromoAsync(), CanUpdatePromo);
    }

    public ICommand GoBackCommand { get; }
    public ObservableCollection<Promo> Promos { get; }
    public ObservableCollection<PromoProduct> ProductResults { get; }
    public ObservableCollection<PromoCategory> CategoryResults { get; }

    public ICommand RefreshPromosCommand { get; }
    public ICommand SearchProductsCommand { get; }
    public ICommand SearchCategoriesCommand { get; }
    public ICommand CreatePromoCommand { get; }
    public ICommand UpdatePromoCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string DateDebutText
    {
        get => _dateDebutText;
        set => SetProperty(ref _dateDebutText, value);
    }

    public string DateFinText
    {
        get => _dateFinText;
        set => SetProperty(ref _dateFinText, value);
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
        ? "Aucune catégorie sélectionnée"
        : $"Catégorie #{SelectedCategory.Id} — {SelectedCategory.DisplayName}";

    public Task InitializeAsync() => LoadPromosAsync();

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
            StatusMessage = "Chargement des promotions…";

            if (!await EnsureSessionAsync())
                return;

            var response = await _apis.PostAsync<object, PromoListResponse>("/promo/list", new { });
            var data = response?.Data ?? new List<Promo>();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Promos.Clear();
                foreach (var promo in data.OrderByDescending(p => p.Id))
                    Promos.Add(promo);

                StatusMessage = Promos.Count == 0
                    ? "Aucune promotion pour le moment."
                    : $"{Promos.Count} promotion(s) chargée(s).";
            });
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[PROMO] http error: {ex.Message}");
            StatusMessage = "Impossible de joindre l'API des promotions.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PROMO] unexpected error: {ex}");
            StatusMessage = "Erreur inattendue lors du chargement.";
        }
        finally
        {
            IsBusy = false;
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
        }
    }

    private bool TryParseForm(out DateTime debut, out DateTime fin, out double prix)
    {
        debut = default;
        fin = default;
        prix = default;

        if (!DateTime.TryParse(DateDebutText, out debut))
        {
            StatusMessage = "Date de début invalide.";
            return false;
        }

        if (!DateTime.TryParse(DateFinText, out fin))
        {
            StatusMessage = "Date de fin invalide.";
            return false;
        }

        if (!double.TryParse(PrixText, out prix))
        {
            StatusMessage = "Prix invalide.";
            return false;
        }

        return true;
    }

    private void ApplyPromoSelection(Promo? promo)
    {
        if (promo is null)
        {
            DateDebutText = string.Empty;
            DateFinText = string.Empty;
            PrixText = string.Empty;
            SelectedProduct = null;
            SelectedCategory = null;
            return;
        }

        DateDebutText = promo.DateDebut?.ToString("o") ?? string.Empty;
        DateFinText = promo.DateFin?.ToString("o") ?? string.Empty;
        PrixText = promo.Prix.ToString("0.##");
        SelectedProduct = promo.LeProduit;
        SelectedCategory = promo.LaCategoriePromo;
    }

    private bool CanCreatePromo()
    {
        return !IsBusy
            && SelectedProduct != null
            && SelectedCategory != null
            && !string.IsNullOrWhiteSpace(DateDebutText)
            && !string.IsNullOrWhiteSpace(DateFinText)
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
    }
}
