using GDM2026.Models;
using GDM2026.Services;
using GDM2026;
using Microsoft.Maui.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class CatalogueViewModel : BaseViewModel
{
    private enum CatalogueFormMode
    {
        None,
        Create,
        Update
    }

    private sealed class CatalogueListResponse
    {
        [JsonProperty("data")]
        public List<Catalogue>? Data { get; set; }
    }

    private sealed class CatalogueResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("data")]
        public Catalogue? Data { get; set; }
    }

    private sealed class ApiMessageResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }
    }

    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionReady;
    private CatalogueFormMode _currentMode;
    private bool _isLoadingCatalogues;
    private string _statusMessage = "Choisissez une action pour commencer.";
    private string _formHeader = "Formulaire";
    private string _formHelperMessage = string.Empty;
    private string _cataloguesStatusMessage = "Les catalogues ne sont pas encore chargés.";

    private Catalogue? _selectedCatalogue;
    private string _mois = string.Empty;
    private string _anneeText = string.Empty;
    private string _url = string.Empty;

    public CatalogueViewModel()
    {
        Catalogues = new ObservableCollection<Catalogue>();

        ShowCreatePanelCommand = new Command(() => SetMode(CatalogueFormMode.Create));
        ShowUpdatePanelCommand = new Command(() => SetMode(CatalogueFormMode.Update));
        RefreshCataloguesCommand = new Command(async () => await LoadCataloguesAsync());
        CreateCatalogueCommand = new Command(async () => await CreateCatalogueAsync(), CanCreateCatalogue);
        UpdateCatalogueCommand = new Command(async () => await UpdateCatalogueAsync(), CanUpdateCatalogue);
        DeleteCatalogueCommand = new Command(async () => await DeleteCatalogueAsync(), CanDeleteCatalogue);
        GoBackCommand = new Command(async () => await NavigateBackAsync());
    }

    public ObservableCollection<Catalogue> Catalogues { get; }

    public ICommand ShowCreatePanelCommand { get; }
    public ICommand ShowUpdatePanelCommand { get; }
    public ICommand RefreshCataloguesCommand { get; }
    public ICommand CreateCatalogueCommand { get; }
    public ICommand UpdateCatalogueCommand { get; }
    public ICommand DeleteCatalogueCommand { get; }
    public ICommand GoBackCommand { get; }

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

    public string CataloguesStatusMessage
    {
        get => _cataloguesStatusMessage;
        set => SetProperty(ref _cataloguesStatusMessage, value);
    }

    public bool IsCreateMode => _currentMode == CatalogueFormMode.Create;
    public bool IsUpdateMode => _currentMode == CatalogueFormMode.Update;
    public bool IsFormSectionVisible => _currentMode is CatalogueFormMode.Create or CatalogueFormMode.Update;
    public bool IsFormInputEnabled => _currentMode == CatalogueFormMode.Create || SelectedCatalogue is not null;

    public string FormActionButtonText => _currentMode == CatalogueFormMode.Update
        ? "Mettre à jour le catalogue"
        : "Ajouter le catalogue";

    public ICommand FormActionCommand => _currentMode == CatalogueFormMode.Update
        ? UpdateCatalogueCommand
        : CreateCatalogueCommand;

    public bool IsFormActionEnabled => (_currentMode == CatalogueFormMode.Update && CanUpdateCatalogue())
        || (_currentMode == CatalogueFormMode.Create && CanCreateCatalogue());

    public bool IsLoadingCatalogues
    {
        get => _isLoadingCatalogues;
        set
        {
            if (SetProperty(ref _isLoadingCatalogues, value))
            {
                RefreshCommands();
            }
        }
    }

    public Catalogue? SelectedCatalogue
    {
        get => _selectedCatalogue;
        set
        {
            if (SetProperty(ref _selectedCatalogue, value))
            {
                ApplySelection(value);
                OnPropertyChanged(nameof(HasCatalogueSelection));
            }
        }
    }

    public string Mois
    {
        get => _mois;
        set
        {
            if (SetProperty(ref _mois, value))
            {
                RefreshCommands();
            }
        }
    }

    public string AnneeText
    {
        get => _anneeText;
        set
        {
            if (SetProperty(ref _anneeText, value))
            {
                RefreshCommands();
            }
        }
    }

    public string Url
    {
        get => _url;
        set
        {
            if (SetProperty(ref _url, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool HasCatalogueSelection => SelectedCatalogue is not null;

    public async Task OnPageAppearingAsync()
    {
        await EnsureSessionAsync();
        SetMode(CatalogueFormMode.None);
        StatusMessage = "Choisissez une action pour commencer.";
    }

    private async Task EnsureSessionAsync()
    {
        if (_sessionReady)
        {
            return;
        }

        try
        {
            var hasSession = await _sessionService.LoadAsync();
            _apis.SetBearerToken(hasSession ? _sessionService.AuthToken : string.Empty);
            _sessionReady = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CATALOGUE] Erreur lors du chargement de session : {ex}");
        }
    }

    private void SetMode(CatalogueFormMode mode)
    {
        _currentMode = mode;
        OnPropertyChanged(nameof(IsCreateMode));
        OnPropertyChanged(nameof(IsUpdateMode));
        OnPropertyChanged(nameof(IsFormSectionVisible));
        OnPropertyChanged(nameof(IsFormInputEnabled));
        OnPropertyChanged(nameof(FormActionButtonText));
        OnPropertyChanged(nameof(FormActionCommand));
        OnPropertyChanged(nameof(IsFormActionEnabled));

        if (mode == CatalogueFormMode.Create)
        {
            FormHeader = "Nouveau catalogue";
            FormHelperMessage = "Renseignez les informations du catalogue à publier.";
            StatusMessage = "Complétez le formulaire puis validez.";
            ResetForm();
        }
        else if (mode == CatalogueFormMode.Update)
        {
            FormHeader = "Mettre à jour un catalogue";
            FormHelperMessage = "Choisissez un catalogue dans la liste pour préremplir le formulaire.";
            StatusMessage = "Sélectionnez un catalogue puis modifiez ses informations.";
            ResetForm();
            _ = LoadCataloguesAsync();
        }
        else
        {
            FormHeader = "Formulaire";
            FormHelperMessage = string.Empty;
            StatusMessage = "Choisissez une action pour commencer.";
            ResetForm();
        }

        RefreshCommands();
    }

    private void ResetForm()
    {
        Mois = string.Empty;
        AnneeText = string.Empty;
        Url = string.Empty;
        SelectedCatalogue = null;
    }

    private void ApplySelection(Catalogue? selected)
    {
        if (selected is null)
        {
            FormHelperMessage = "Choisissez un catalogue dans la liste pour le modifier.";
            RefreshCommands();
            return;
        }

        Mois = selected.Mois ?? string.Empty;
        AnneeText = selected.Annee == 0 ? string.Empty : selected.Annee.ToString();
        Url = selected.Url ?? string.Empty;
        FormHelperMessage = $"Modification du catalogue #{selected.Id}.";
        RefreshCommands();
    }

    private async Task LoadCataloguesAsync()
    {
        await EnsureSessionAsync();

        IsLoadingCatalogues = true;
        CataloguesStatusMessage = "Chargement des catalogues…";

        try
        {
            var response = await _apis.PostAsync<object, CatalogueListResponse>("/catalogue/list", new { });
            var catalogues = response?.Data ?? new List<Catalogue>();

            Catalogues.Clear();
            foreach (var catalogue in catalogues.OrderByDescending(c => c.Annee).ThenBy(c => c.Mois))
            {
                Catalogues.Add(catalogue);
            }

            CataloguesStatusMessage = Catalogues.Count == 0
                ? "Aucun catalogue chargé pour le moment."
                : $"{Catalogues.Count} catalogue(s) chargé(s).";
        }
        catch (HttpRequestException httpEx)
        {
            CataloguesStatusMessage = "Impossible de charger les catalogues.";
            Debug.WriteLine($"[CATALOGUE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            CataloguesStatusMessage = "Erreur inattendue pendant le chargement.";
            Debug.WriteLine($"[CATALOGUE] Unexpected error: {ex}");
        }
        finally
        {
            IsLoadingCatalogues = false;
        }
    }

    private async Task CreateCatalogueAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!TryParseAnnee(out var annee))
        {
            StatusMessage = "Renseignez un mois, une année et une URL valides.";
            return;
        }

        try
        {
            IsBusy = true;
            await EnsureSessionAsync();

            var payload = new
            {
                mois = Mois?.Trim(),
                annee = annee,
                url = Url?.Trim(),
            };

            var response = await _apis.PostAsync<object, CatalogueResponse>("/catalogue/create", payload);
            var created = response?.Data ?? new Catalogue
            {
                Id = 0,
                Mois = Mois,
                Annee = annee,
                Url = Url,
            };

            Catalogues.Insert(0, created);
            CataloguesStatusMessage = Catalogues.Count == 0
                ? "Aucun catalogue chargé pour le moment."
                : $"{Catalogues.Count} catalogue(s) chargé(s).";
            StatusMessage = response?.Message ?? "Catalogue créé avec succès.";
            ResetForm();
        }
        catch (HttpRequestException httpEx)
        {
            StatusMessage = "Impossible de créer le catalogue.";
            Debug.WriteLine($"[CATALOGUE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur inattendue lors de la création.";
            Debug.WriteLine($"[CATALOGUE] Unexpected error: {ex}");
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task UpdateCatalogueAsync()
    {
        if (IsBusy || SelectedCatalogue is null)
        {
            return;
        }

        if (!TryParseAnnee(out var annee))
        {
            StatusMessage = "Renseignez un mois, une année et une URL valides.";
            return;
        }

        try
        {
            IsBusy = true;
            await EnsureSessionAsync();

            var payload = new
            {
                id = SelectedCatalogue.Id,
                mois = Mois?.Trim(),
                annee = annee,
                url = Url?.Trim(),
            };

            var response = await _apis.PostAsync<object, CatalogueResponse>("/catalogue/update", payload);
            var updated = response?.Data;

            var targetId = SelectedCatalogue.Id;
            var existing = Catalogues.FirstOrDefault(c => c.Id == targetId);
            if (existing is not null)
            {
                existing.Mois = updated?.Mois ?? Mois;
                existing.Annee = updated?.Annee ?? annee;
                existing.Url = updated?.Url ?? Url;
                SelectedCatalogue = existing;
            }

            StatusMessage = response?.Message ?? "Catalogue mis à jour.";
        }
        catch (HttpRequestException httpEx)
        {
            StatusMessage = "Impossible de mettre à jour ce catalogue.";
            Debug.WriteLine($"[CATALOGUE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur inattendue lors de la mise à jour.";
            Debug.WriteLine($"[CATALOGUE] Unexpected error: {ex}");
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task DeleteCatalogueAsync()
    {
        if (IsBusy || SelectedCatalogue is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await EnsureSessionAsync();

            var payload = new { id = SelectedCatalogue.Id };
            var response = await _apis.PostAsync<object, ApiMessageResponse>("/catalogue/delete", payload);

            var toRemove = SelectedCatalogue;
            SelectedCatalogue = null;
            Catalogues.Remove(toRemove);
            CataloguesStatusMessage = Catalogues.Count == 0
                ? "Aucun catalogue chargé pour le moment."
                : $"{Catalogues.Count} catalogue(s) chargé(s).";
            StatusMessage = response?.Message ?? "Catalogue supprimé.";
        }
        catch (HttpRequestException httpEx)
        {
            StatusMessage = "Impossible de supprimer ce catalogue.";
            Debug.WriteLine($"[CATALOGUE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur inattendue lors de la suppression.";
            Debug.WriteLine($"[CATALOGUE] Unexpected error: {ex}");
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private bool TryParseAnnee(out int annee)
    {
        var hasYear = int.TryParse(AnneeText?.Trim(), out annee);
        return !string.IsNullOrWhiteSpace(Mois) && hasYear && annee > 0 && !string.IsNullOrWhiteSpace(Url);
    }

    private async Task NavigateBackAsync()
    {
        if (Shell.Current?.Navigation?.NavigationStack?.Count > 1)
        {
            await Shell.Current.GoToAsync("..", animate: true);
        }
        else if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync(nameof(HomePage), animate: true);
        }
    }

    private bool CanCreateCatalogue()
    {
        return !IsBusy && !IsLoadingCatalogues && TryParseAnnee(out _);
    }

    private bool CanUpdateCatalogue()
    {
        return !IsBusy && !IsLoadingCatalogues && SelectedCatalogue is not null && TryParseAnnee(out _);
    }

    private bool CanDeleteCatalogue()
    {
        return !IsBusy && !IsLoadingCatalogues && SelectedCatalogue is not null;
    }

    private void RefreshCommands()
    {
        (CreateCatalogueCommand as Command)?.ChangeCanExecute();
        (UpdateCatalogueCommand as Command)?.ChangeCanExecute();
        (DeleteCatalogueCommand as Command)?.ChangeCanExecute();
        OnPropertyChanged(nameof(IsFormActionEnabled));
    }
}
