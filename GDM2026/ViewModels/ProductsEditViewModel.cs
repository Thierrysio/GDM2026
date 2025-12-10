using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

    public ProductsEditViewModel()
    {
        VisibleProducts = new ObservableCollection<ProductCatalogItem>();
        SearchProductsCommand = new Command(async () => await SearchAsync());
        LoadMoreCommand = new Command(async () => await LoadNextPageAsync());
    }

    public ObservableCollection<ProductCatalogItem> VisibleProducts { get; }

    public ICommand SearchProductsCommand { get; }

    public ICommand LoadMoreCommand { get; }

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
                ? "Chargement des produits…"
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
            StatusMessage = "Chargement supplémentaire…";

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
}
