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

    private string _statusMessage = "Choisissez un mode pour commencer.";
    private string _newCategoryName = string.Empty;
    private string _editCategoryName = string.Empty;

    private bool _isRefreshing;
    private PromoCategory? _selectedCategory;
    private bool _isSelectionVisible;
    private bool _isCreateMode;
    private bool _isUpdateMode;

    private sealed class PromoCategoryListResponse
    {
        [JsonProperty("data")]
        public List<PromoCategory>? Data { get; set; }
    }

    public EvenementPageViewModel()
    {
        PromoCategories = new ObservableCollection<PromoCategory>();

        RefreshCommand = new Command(async () => await RefreshListAsync());

        ShowCreateModeCommand = new Command(() => ActivateCreateMode(), () => !IsBusy);
        ShowUpdateModeCommand = new Command(async () => await ActivateUpdateModeAsync(), () => !IsBusy);

        CreateCommand = new Command(async () => await CreateCategoryAsync(), CanCreateCategory);
        UpdateCommand = new Command(async () => await UpdateCategoryAsync(), CanUpdateCategory);
        DeleteCommand = new Command(async () => await DeleteCategoryAsync(), CanDeleteCategory);

        ReloadSelectionCommand = new Command(async () => await LoadCategoriesAsync(forceReload: true));
    }

    public ObservableCollection<PromoCategory> PromoCategories { get; }

    public ICommand RefreshCommand { get; }
    public ICommand ShowCreateModeCommand { get; }
    public ICommand ShowUpdateModeCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ReloadSelectionCommand { get; }

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
                RefreshCommandStates();
        }
    }

    public string EditCategoryName
    {
        get => _editCategoryName;
        set
        {
            if (SetProperty(ref _editCategoryName, value))
                RefreshCommandStates();
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
                OnPropertyChanged(nameof(SelectionButtonText));
        }
    }

    public bool IsCreateMode
    {
        get => _isCreateMode;
        private set
        {
            if (SetProperty(ref _isCreateMode, value))
            {
                OnPropertyChanged(nameof(IsFormSectionVisible));
                OnPropertyChanged(nameof(FormHeader));
                OnPropertyChanged(nameof(FormHelperMessage));
            }
        }
    }

    public bool IsUpdateMode
    {
        get => _isUpdateMode;
        private set
        {
            if (SetProperty(ref _isUpdateMode, value))
            {
                OnPropertyChanged(nameof(IsFormSectionVisible));
                OnPropertyChanged(nameof(FormHeader));
                OnPropertyChanged(nameof(FormHelperMessage));
            }
        }
    }

    public bool IsFormSectionVisible => IsCreateMode || IsUpdateMode;

    public string FormHeader => IsCreateMode
        ? "Créer un événement"
        : IsUpdateMode
            ? "Mettre à jour un événement"
            : string.Empty;

    public string FormHelperMessage => IsCreateMode
        ? "Complétez le nom puis validez pour créer l'événement."
        : IsUpdateMode
            ? "Sélectionnez un événement à gauche puis modifiez son titre."
            : "Choisissez un mode pour afficher le formulaire.";

    public string SelectionButtonText => !_categoriesLoaded
        ? "Afficher les événements"
        : "Recharger les événements";

    public Task InitializeAsync()
    {
        // ✅ Pas de chargement ici
        StatusMessage = "Choisissez Créer ou Mettre à jour pour commencer.";
        return Task.CompletedTask;
    }

    private void ActivateCreateMode()
    {
        if (IsBusy)
            return;

        IsCreateMode = true;
        IsUpdateMode = false;
        IsSelectionVisible = false;
        SelectedCategory = null;
        EditCategoryName = string.Empty;
        _categoriesLoaded = false;
        StatusMessage = "Complétez le nom pour créer un événement.";
        RefreshCommandStates();
        RefreshModeCommands();
        OnPropertyChanged(nameof(SelectionButtonText));
    }

    private async Task ActivateUpdateModeAsync()
    {
        if (IsBusy)
            return;

        IsUpdateMode = true;
        IsCreateMode = false;
        IsSelectionVisible = true;
        StatusMessage = "Appuyez sur « Afficher les événements » pour charger la liste.";
        RefreshModeCommands();
        RefreshCommandStates();

        if (!_categoriesLoaded && PromoCategories.Count == 0)
        {
            await LoadCategoriesAsync();
        }
    }

    private async Task RefreshListAsync()
    {
        if (!IsSelectionVisible)
        {
            IsRefreshing = false;
            return;
        }

        IsRefreshing = true;
        await LoadCategoriesAsync(forceReload: true);
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
            RefreshCommandStates();
            RefreshModeCommands();
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
                PromoCategories.Add(category);

            _categoriesLoaded = true;
            OnPropertyChanged(nameof(SelectionButtonText));

            if (PromoCategories.Count == 0)
            {
                StatusMessage = "Aucun événement pour le moment.";
                SelectedCategory = null;
                return;
            }

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
            RefreshModeCommands();
        }
    }

    private async Task CreateCategoryAsync()
    {
        if (!IsCreateMode)
        {
            StatusMessage = "Passez en mode création pour ajouter un événement.";
            return;
        }

        if (!CanCreateCategory())
        {
            StatusMessage = "Renseignez le nom de l'événement à créer.";
            return;
        }

        try
        {
            IsBusy = true;
            RefreshCommandStates();
            RefreshModeCommands();

            if (!await EnsureSessionAsync())
            {
                StatusMessage = "Session expirée. Reconnectez-vous pour créer.";
                return;
            }

            var payload = new { nom = NewCategoryName.Trim() };
            var created = await _apis.PostBoolAsync("/categorie/promo/create", payload);

            if (!created)
            {
                StatusMessage = "La création a échoué.";
                return;
            }

            NewCategoryName = string.Empty;
            StatusMessage = "Événement créé avec succès.";
            await ShowInfoAsync("Création", "Événement créé avec succès.");

            // Recharge uniquement si la liste est ouverte
            if (IsSelectionVisible)
            {
                await LoadCategoriesAsync(forceReload: true);
            }
            else
            {
                _categoriesLoaded = false; // pour forcer reload quand on ouvrira
                OnPropertyChanged(nameof(SelectionButtonText));
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
            RefreshModeCommands();
        }
    }

    private async Task UpdateCategoryAsync()
    {
        if (!IsUpdateMode)
        {
            StatusMessage = "Passez en mode mise à jour pour modifier un événement.";
            return;
        }

        if (!CanUpdateCategory())
        {
            StatusMessage = "Sélectionnez un événement et renseignez son nouveau nom.";
            return;
        }

        try
        {
            IsBusy = true;
            RefreshCommandStates();
            RefreshModeCommands();

            if (!await EnsureSessionAsync())
            {
                StatusMessage = "Session expirée. Reconnectez-vous pour modifier.";
                return;
            }

            var newName = EditCategoryName.Trim();

            var payload = new
            {
                id = SelectedCategory!.Id,
                nom = newName
            };

            var updated = await _apis.PostBoolAsync("/categorie/promo/update", payload);

            if (!updated)
            {
                StatusMessage = "La mise à jour a échoué.";
                return;
            }

            StatusMessage = "Événement mis à jour.";
            await ShowInfoAsync("Mise à jour", "Événement mis à jour avec succès.");

            if (IsSelectionVisible)
            {
                await LoadCategoriesAsync(forceReload: true);
            }
            else
            {
                _categoriesLoaded = false;
                OnPropertyChanged(nameof(SelectionButtonText));
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
            RefreshModeCommands();
        }
    }

    private async Task DeleteCategoryAsync()
    {
        if (!IsUpdateMode)
        {
            StatusMessage = "Passez en mode mise à jour pour supprimer un événement.";
            return;
        }

        if (!CanDeleteCategory())
        {
            StatusMessage = "Sélectionnez un événement à supprimer.";
            return;
        }

        var target = SelectedCategory!;
        var ok = await ConfirmAsync("Suppression", $"Supprimer l'événement \"{target.DisplayName}\" ?");
        if (!ok)
            return;

        try
        {
            IsBusy = true;
            RefreshCommandStates();
            RefreshModeCommands();

            if (!await EnsureSessionAsync())
            {
                StatusMessage = "Session expirée. Reconnectez-vous pour supprimer.";
                return;
            }

            var payload = new { id = target.Id };
            var deleted = await _apis.PostBoolAsync("/categorie/promo/delete", payload);

            if (!deleted)
            {
                StatusMessage = "La suppression a échoué.";
                return;
            }

            // ✅ Disparition immédiate si la liste est ouverte
            if (IsSelectionVisible)
            {
                var toRemove = PromoCategories.FirstOrDefault(c => c.Id == target.Id);
                if (toRemove is not null)
                    PromoCategories.Remove(toRemove);
            }
            else
            {
                _categoriesLoaded = false;
                OnPropertyChanged(nameof(SelectionButtonText));
            }

            SelectedCategory = null;
            EditCategoryName = string.Empty;

            StatusMessage = "Événement supprimé.";
            await ShowInfoAsync("Suppression", "Événement supprimé avec succès.");

            if (IsSelectionVisible && PromoCategories.Count == 0)
                StatusMessage = "Aucun événement pour le moment.";
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
            RefreshModeCommands();
        }
    }

    public bool IsFormInputEnabled => !IsBusy;

    private bool CanCreateCategory() => !IsBusy && IsCreateMode && !string.IsNullOrWhiteSpace(NewCategoryName);
    private bool CanUpdateCategory() => !IsBusy && IsUpdateMode && SelectedCategory is not null && !string.IsNullOrWhiteSpace(EditCategoryName);
    private bool CanDeleteCategory() => !IsBusy && IsUpdateMode && SelectedCategory is not null;

    private void RefreshCommandStates()
    {
        (CreateCommand as Command)?.ChangeCanExecute();
        (UpdateCommand as Command)?.ChangeCanExecute();
        (DeleteCommand as Command)?.ChangeCanExecute();
    }

    private void RefreshModeCommands()
    {
        (ShowCreateModeCommand as Command)?.ChangeCanExecute();
        (ShowUpdateModeCommand as Command)?.ChangeCanExecute();
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

    private Task ShowInfoAsync(string title, string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null)
            {
                return;
            }

            await page.DisplayAlert(title, message, "OK");
        });
    }

    private Task<bool> ConfirmAsync(string title, string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null)
            {
                return true;
            }

            return await page.DisplayAlert(title, message, "Oui", "Non");
        });
    }
}
