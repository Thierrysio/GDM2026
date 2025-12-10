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

public class EvenementPageViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionLoaded;
    private bool _categoriesLoaded;
    private string _statusMessage = "Chargez les catégories promo pour commencer.";
    private string _newCategoryName = string.Empty;
    private string _editCategoryName = string.Empty;
    private bool _isRefreshing;
    private PromoCategory? _selectedCategory;
    private bool _isSelectionVisible;

    private sealed class PromoCategoryListResponse
    {
        [JsonProperty("data")]
        public List<PromoCategory>? Data { get; set; }
    }

    public EvenementPageViewModel()
    {
        PromoCategories = new ObservableCollection<PromoCategory>();

        RefreshCommand = new Command(async () =>
        {
            IsRefreshing = true;
            await LoadCategoriesAsync(forceReload: true);
        });

        CreateCommand = new Command(async () => await CreateCategoryAsync(), CanCreateCategory);
        UpdateCommand = new Command(async () => await UpdateCategoryAsync(), CanUpdateCategory);
        DeleteCommand = new Command(async () => await DeleteCategoryAsync(), CanDeleteCategory);
        ShowSelectionCommand = new Command(async () => await ToggleSelectionAsync());
    }

    public ObservableCollection<PromoCategory> PromoCategories { get; }

    public ICommand RefreshCommand { get; }

    public ICommand CreateCommand { get; }

    public ICommand UpdateCommand { get; }

    public ICommand DeleteCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string NewCategoryName
    {
        get => _newCategoryName;
        set
        {
            if (SetProperty(ref _newCategoryName, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public string EditCategoryName
    {
        get => _editCategoryName;
        set
        {
            if (SetProperty(ref _editCategoryName, value))
            {
                RefreshCommandStates();
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
                EditCategoryName = value?.Name ?? string.Empty;
                OnPropertyChanged(nameof(SelectedCategoryName));
                RefreshCommandStates();
            }
        }
    }

    public string SelectedCategoryName => SelectedCategory is null
        ? "Aucune catégorie sélectionnée."
        : $"#{SelectedCategory.Id} — {SelectedCategory.DisplayName}";

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public ICommand ShowSelectionCommand { get; }

    public bool IsSelectionVisible
    {
        get => _isSelectionVisible;
        set
        {
            if (SetProperty(ref _isSelectionVisible, value))
            {
                OnPropertyChanged(nameof(SelectionButtonText));
            }
        }
    }

    public string SelectionButtonText => IsSelectionVisible
        ? "Masquer la sélection"
        : "Choisir une catégorie";

    public Task InitializeAsync()
    {
        StatusMessage = "Cliquez sur \"Choisir une catégorie\" pour charger les données.";
        return Task.CompletedTask;
    }

    private async Task LoadCategoriesAsync(bool forceReload = false)
    {
        if (IsBusy)
        {
            return;
        }

        if (!forceReload && _categoriesLoaded)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Chargement des catégories promo…";

            if (!await EnsureSessionAsync())
            {
                StatusMessage = "Session expirée. Reconnectez-vous pour gérer les catégories.";
                return;
            }

            var response = await _apis.PostAsync<object, PromoCategoryListResponse>("/categorie/promo/list", new { });
            var items = response?.Data ?? new List<PromoCategory>();

            PromoCategories.Clear();
            foreach (var category in items.OrderBy(c => c.Name))
            {
                PromoCategories.Add(category);
            }

            if (PromoCategories.Count == 0)
            {
                StatusMessage = "Aucune catégorie promo pour le moment.";
            }
            else
            {
                StatusMessage = $"{PromoCategories.Count} catégorie(s) promo chargée(s).";
            }
            _categoriesLoaded = true;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[EVENEMENT] HTTP error: {ex}");
            StatusMessage = "Impossible de contacter l'API des catégories promo.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EVENEMENT] load error: {ex}");
            StatusMessage = "Erreur inattendue lors du chargement.";
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    private async Task CreateCategoryAsync()
    {
        if (!CanCreateCategory())
        {
            StatusMessage = "Renseignez le nom de la catégorie à créer.";
            return;
        }

        try
        {
            IsBusy = true;
            RefreshCommandStates();

            if (!await EnsureSessionAsync())
            {
                StatusMessage = "Session expirée. Reconnectez-vous pour créer.";
                return;
            }

            var payload = new { nom = NewCategoryName.Trim() };
            var created = await _apis.PostBoolAsync("/categorie/promo/create", payload);

            StatusMessage = created
                ? "Catégorie promo créée avec succès."
                : "La création a échoué.";

            if (created)
            {
                NewCategoryName = string.Empty;
                await ShowConfirmationAsync("Catégorie promo créée avec succès.");
                await LoadCategoriesAsync(forceReload: true);
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[EVENEMENT] create error: {ex}");
            StatusMessage = "Impossible de créer la catégorie.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EVENEMENT] unexpected create error: {ex}");
            StatusMessage = "Erreur inattendue lors de la création.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommandStates();
        }
    }

    private async Task UpdateCategoryAsync()
    {
        if (!CanUpdateCategory())
        {
            StatusMessage = "Sélectionnez une catégorie et renseignez son nouveau nom.";
            return;
        }

        try
        {
            IsBusy = true;
            RefreshCommandStates();

            if (!await EnsureSessionAsync())
            {
                StatusMessage = "Session expirée. Reconnectez-vous pour modifier.";
                return;
            }

            var payload = new
            {
                id = SelectedCategory!.Id,
                nom = EditCategoryName.Trim()
            };

            var updated = await _apis.PostBoolAsync("/categorie/promo/update", payload);
            StatusMessage = updated
                ? "Catégorie promo mise à jour."
                : "La mise à jour a échoué.";

            if (updated)
            {
                await LoadCategoriesAsync(forceReload: true);
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[EVENEMENT] update error: {ex}");
            StatusMessage = "Impossible de mettre à jour la catégorie.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EVENEMENT] unexpected update error: {ex}");
            StatusMessage = "Erreur inattendue lors de la mise à jour.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommandStates();
        }
    }

    private async Task DeleteCategoryAsync()
    {
        if (!CanDeleteCategory())
        {
            StatusMessage = "Sélectionnez une catégorie à supprimer.";
            return;
        }

        try
        {
            IsBusy = true;
            RefreshCommandStates();

            if (!await EnsureSessionAsync())
            {
                StatusMessage = "Session expirée. Reconnectez-vous pour supprimer.";
                return;
            }

            var payload = new { id = SelectedCategory!.Id };
            var deleted = await _apis.PostBoolAsync("/categorie/promo/delete", payload);

            StatusMessage = deleted
                ? "Catégorie promo supprimée."
                : "La suppression a échoué.";

            if (deleted)
            {
                SelectedCategory = null;
                EditCategoryName = string.Empty;
                await LoadCategoriesAsync(forceReload: true);
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[EVENEMENT] delete error: {ex}");
            StatusMessage = "Impossible de supprimer la catégorie.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EVENEMENT] unexpected delete error: {ex}");
            StatusMessage = "Erreur inattendue lors de la suppression.";
        }
        finally
        {
            IsBusy = false;
            RefreshCommandStates();
        }
    }

    private bool CanCreateCategory() => !IsBusy && !string.IsNullOrWhiteSpace(NewCategoryName);

    private bool CanUpdateCategory() => !IsBusy
        && SelectedCategory is not null
        && !string.IsNullOrWhiteSpace(EditCategoryName);

    private bool CanDeleteCategory() => !IsBusy && SelectedCategory is not null;

    private void RefreshCommandStates()
    {
        (CreateCommand as Command)?.ChangeCanExecute();
        (UpdateCommand as Command)?.ChangeCanExecute();
        (DeleteCommand as Command)?.ChangeCanExecute();
    }

    private async Task<bool> EnsureSessionAsync()
    {
        if (!_sessionLoaded)
        {
            _sessionLoaded = true;
            await _sessionService.LoadAsync();
        }

        if (!string.IsNullOrWhiteSpace(_sessionService.AuthToken))
        {
            _apis.SetBearerToken(_sessionService.AuthToken);
            return true;
        }

        return false;
    }

    private async Task ToggleSelectionAsync()
    {
        IsSelectionVisible = !IsSelectionVisible;

        if (IsSelectionVisible)
        {
            await LoadCategoriesAsync();
        }
    }

    private Task ShowConfirmationAsync(string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
            await DialogService.DisplayAlertAsync("Confirmation", message, "OK"));
    }
}
