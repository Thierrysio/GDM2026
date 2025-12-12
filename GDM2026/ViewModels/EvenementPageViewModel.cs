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

    private string _statusMessage = "Chargez les événements pour commencer.";
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

        // IMPORTANT : sécurise la sélection et déclenche bien le VisualState "Selected"
        SelectionChangedCommand = new Command<PromoCategory?>(OnSelectionChanged);
    }

    public ObservableCollection<PromoCategory> PromoCategories { get; }

    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ShowSelectionCommand { get; }

    // À binder dans le CollectionView : SelectionChangedCommand="{Binding SelectionChangedCommand}"
    public ICommand SelectionChangedCommand { get; }

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
                OnPropertyChanged(nameof(HasCategorySelection));
                RefreshCommandStates();
            }
        }
    }

    public string SelectedCategoryName => SelectedCategory is null
        ? "Aucun événement sélectionné."
        : $"#{SelectedCategory.Id} — {SelectedCategory.DisplayName}";

    public bool HasCategorySelection => SelectedCategory is not null;

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

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
        ? "Masquer les événements"
        : "Modifier";

    public Task InitializeAsync()
    {
        StatusMessage = "Cliquez sur \"Modifier\" pour charger les données.";
        return Task.CompletedTask;
    }

    private void OnSelectionChanged(PromoCategory? selected)
    {
        SelectedCategory = selected;

        if (selected is null)
        {
            StatusMessage = "Aucun événement sélectionné.";
            return;
        }

        // UX : on préremplit et on annonce clairement
        EditCategoryName = selected.Name ?? string.Empty;
        StatusMessage = $"Sélection : #{selected.Id} — {selected.DisplayName}";
    }

    private async Task LoadCategoriesAsync(bool forceReload = false)
    {
        if (IsBusy)
            return;

        if (!forceReload && _categoriesLoaded)
            return;

        int? keepSelectedId = SelectedCategory?.Id;

        try
        {
            IsBusy = true;
            StatusMessage = "Chargement des événements…";

            if (!await EnsureSessionAsync())
            {
                StatusMessage = "Session expirée. Reconnectez-vous pour gérer les événements.";
                return;
            }

            var response = await _apis.PostAsync<object, PromoCategoryListResponse>("/categorie/promo/list", new { });
            var items = response?.Data ?? new List<PromoCategory>();

            PromoCategories.Clear();
            foreach (var category in items.OrderBy(c => c.Name))
            {
                PromoCategories.Add(category);
            }

            _categoriesLoaded = true;

            if (PromoCategories.Count == 0)
            {
                StatusMessage = "Aucun événement pour le moment.";
                SelectedCategory = null;
                return;
            }

            // IMPORTANT : après reload, l'instance SelectedCategory n'existe plus
            // => on re-sélectionne par Id pour conserver la surbrillance / editpanel
            if (keepSelectedId.HasValue)
            {
                var match = PromoCategories.FirstOrDefault(c => c.Id == keepSelectedId.Value);
                SelectedCategory = match;
            }

            StatusMessage = $"{PromoCategories.Count} événement(s) chargé(s).";
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[EVENEMENT] HTTP error: {ex}");
            StatusMessage = "Impossible de contacter l'API des événements.";
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
            RefreshCommandStates();
        }
    }

    private async Task CreateCategoryAsync()
    {
        if (!CanCreateCategory())
        {
            StatusMessage = "Renseignez le nom de l'événement à créer.";
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

            StatusMessage = created ? "Événement créé avec succès." : "La création a échoué.";

            if (created)
            {
                NewCategoryName = string.Empty;
                await ShowConfirmationAsync("Événement créé avec succès.");
                await LoadCategoriesAsync(forceReload: true);
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[EVENEMENT] create error: {ex}");
            StatusMessage = "Impossible de créer l'événement.";
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
            StatusMessage = "Sélectionnez un événement et renseignez son nouveau nom.";
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
            StatusMessage = updated ? "Événement mis à jour." : "La mise à jour a échoué.";

            if (updated)
            {
                await LoadCategoriesAsync(forceReload: true);
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[EVENEMENT] update error: {ex}");
            StatusMessage = "Impossible de mettre à jour l'événement.";
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
            StatusMessage = "Sélectionnez un événement à supprimer.";
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

            StatusMessage = deleted ? "Événement supprimé." : "La suppression a échoué.";

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
            StatusMessage = "Impossible de supprimer l'événement.";
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
