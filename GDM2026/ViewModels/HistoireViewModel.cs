using GDM2026.Models;
using GDM2026.Services;
using GDM2026.Views;
using Microsoft.Maui.Controls;
using Newtonsoft.Json;
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

public class HistoireViewModel : BaseViewModel
{
    private enum HistoireFormMode
    {
        None,
        Create,
        Update
    }

    private sealed class HistoireListResponse
    {
        [JsonProperty("data")]
        public List<Histoire>? Data { get; set; }
    }

    private sealed class HistoireResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("data")]
        public Histoire? Data { get; set; }
    }

    private sealed class ApiMessageResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }
    }

    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionReady;
    private HistoireFormMode _currentMode;
    private bool _isLoadingHistoires;
    private string _statusMessage = "Choisissez une action pour commencer.";
    private string _formHeader = "Formulaire";
    private string _formHelperMessage = string.Empty;
    private string _histoiresStatusMessage = "Les histoires ne sont pas encore chargées.";

    private Histoire? _selectedHistoire;
    private string _titre = string.Empty;
    private string _texte = string.Empty;
    private string _urlImage = string.Empty;
    private string _dateHistoireText = string.Empty;

    public HistoireViewModel()
    {
        Histoires = new ObservableCollection<Histoire>();

        ShowCreatePanelCommand = new Command(() => SetMode(HistoireFormMode.Create));
        ShowUpdatePanelCommand = new Command(() => SetMode(HistoireFormMode.Update));
        RefreshHistoiresCommand = new Command(async () => await LoadHistoiresAsync());
        CreateHistoireCommand = new Command(async () => await CreateHistoireAsync(), CanCreateHistoire);
        UpdateHistoireCommand = new Command(async () => await UpdateHistoireAsync(), CanUpdateHistoire);
        DeleteHistoireCommand = new Command(async () => await DeleteHistoireAsync(), CanDeleteHistoire);
        GoBackCommand = new Command(async () => await NavigateBackAsync());
    }

    public ObservableCollection<Histoire> Histoires { get; }

    public ICommand ShowCreatePanelCommand { get; }
    public ICommand ShowUpdatePanelCommand { get; }
    public ICommand RefreshHistoiresCommand { get; }
    public ICommand CreateHistoireCommand { get; }
    public ICommand UpdateHistoireCommand { get; }
    public ICommand DeleteHistoireCommand { get; }
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

    public string HistoiresStatusMessage
    {
        get => _histoiresStatusMessage;
        set => SetProperty(ref _histoiresStatusMessage, value);
    }

    public bool IsCreateMode => _currentMode == HistoireFormMode.Create;
    public bool IsUpdateMode => _currentMode == HistoireFormMode.Update;
    public bool IsFormSectionVisible => _currentMode is HistoireFormMode.Create or HistoireFormMode.Update;
    public bool IsFormInputEnabled => _currentMode == HistoireFormMode.Create || SelectedHistoire is not null;

    public string FormActionButtonText => _currentMode == HistoireFormMode.Update
        ? "Mettre à jour l'histoire"
        : "Ajouter l'histoire";

    public ICommand FormActionCommand => _currentMode == HistoireFormMode.Update
        ? UpdateHistoireCommand
        : CreateHistoireCommand;

    public bool IsFormActionEnabled => (_currentMode == HistoireFormMode.Update && CanUpdateHistoire())
        || (_currentMode == HistoireFormMode.Create && CanCreateHistoire());

    public bool IsLoadingHistoires
    {
        get => _isLoadingHistoires;
        set
        {
            if (SetProperty(ref _isLoadingHistoires, value))
            {
                RefreshCommands();
            }
        }
    }

    public Histoire? SelectedHistoire
    {
        get => _selectedHistoire;
        set
        {
            if (SetProperty(ref _selectedHistoire, value))
            {
                ApplySelection(value);
                OnPropertyChanged(nameof(HasHistoireSelection));
            }
        }
    }

    public string Titre
    {
        get => _titre;
        set
        {
            if (SetProperty(ref _titre, value))
            {
                RefreshCommands();
            }
        }
    }

    public string Texte
    {
        get => _texte;
        set
        {
            if (SetProperty(ref _texte, value))
            {
                RefreshCommands();
            }
        }
    }

    public string UrlImage
    {
        get => _urlImage;
        set
        {
            if (SetProperty(ref _urlImage, value))
            {
                RefreshCommands();
            }
        }
    }

    public string DateHistoireText
    {
        get => _dateHistoireText;
        set
        {
            if (SetProperty(ref _dateHistoireText, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool HasHistoireSelection => SelectedHistoire is not null;

    public async Task OnPageAppearingAsync()
    {
        await EnsureSessionAsync();
        SetMode(HistoireFormMode.None);
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
            Debug.WriteLine($"[HISTOIRE] Erreur lors du chargement de session : {ex}");
        }
    }

    private void SetMode(HistoireFormMode mode)
    {
        _currentMode = mode;
        OnPropertyChanged(nameof(IsCreateMode));
        OnPropertyChanged(nameof(IsUpdateMode));
        OnPropertyChanged(nameof(IsFormSectionVisible));
        OnPropertyChanged(nameof(IsFormInputEnabled));
        OnPropertyChanged(nameof(FormActionButtonText));
        OnPropertyChanged(nameof(FormActionCommand));
        OnPropertyChanged(nameof(IsFormActionEnabled));

        if (mode == HistoireFormMode.Create)
        {
            FormHeader = "Nouvelle histoire";
            FormHelperMessage = "Renseignez les informations de l'histoire.";
            StatusMessage = "Complétez le formulaire puis validez.";
            ResetForm();
        }
        else if (mode == HistoireFormMode.Update)
        {
            FormHeader = "Mettre à jour une histoire";
            FormHelperMessage = "Choisissez une histoire dans la liste pour préremplir le formulaire.";
            StatusMessage = "Sélectionnez une histoire puis modifiez ses informations.";
            ResetForm();
            _ = LoadHistoiresAsync();
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
        Titre = string.Empty;
        Texte = string.Empty;
        UrlImage = string.Empty;
        DateHistoireText = string.Empty;
        SelectedHistoire = null;
    }

    private void ApplySelection(Histoire? selected)
    {
        if (selected is null)
        {
            FormHelperMessage = "Choisissez une histoire dans la liste pour la modifier.";
            RefreshCommands();
            return;
        }

        Titre = selected.Titre ?? string.Empty;
        Texte = selected.Texte ?? string.Empty;
        UrlImage = selected.UrlImage ?? string.Empty;
        DateHistoireText = selected.DateHistoire?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
        FormHelperMessage = $"Modification de l'histoire #{selected.Id}.";
        RefreshCommands();
    }

    private async Task LoadHistoiresAsync()
    {
        await EnsureSessionAsync();

        IsLoadingHistoires = true;
        HistoiresStatusMessage = "Chargement des histoires…";

        try
        {
            var response = await _apis.PostAsync<object, HistoireListResponse>("/histoire/list", new { });
            var histoires = response?.Data ?? new List<Histoire>();

            Histoires.Clear();
            foreach (var histoire in histoires.OrderByDescending(h => h.DateHistoire ?? DateTime.MinValue))
            {
                Histoires.Add(histoire);
            }

            HistoiresStatusMessage = Histoires.Count == 0
                ? "Aucune histoire chargée pour le moment."
                : $"{Histoires.Count} histoire(s) chargée(s).";
        }
        catch (HttpRequestException httpEx)
        {
            HistoiresStatusMessage = "Impossible de charger les histoires.";
            Debug.WriteLine($"[HISTOIRE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            HistoiresStatusMessage = "Erreur inattendue pendant le chargement.";
            Debug.WriteLine($"[HISTOIRE] Unexpected error: {ex}");
        }
        finally
        {
            IsLoadingHistoires = false;
        }
    }

    private async Task CreateHistoireAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!TryParseDate(out var parsedDate))
        {
            StatusMessage = "Renseignez un titre, un texte, une image et une date valides.";
            return;
        }

        try
        {
            IsBusy = true;
            await EnsureSessionAsync();

            var payload = new
            {
                titre = Titre?.Trim(),
                texte = Texte?.Trim(),
                urlImage = UrlImage?.Trim(),
                dateHistoire = parsedDate.ToString("O", CultureInfo.InvariantCulture),
            };

            var response = await _apis.PostAsync<object, HistoireResponse>("/histoire/create", payload);
            var created = response?.Data ?? new Histoire
            {
                Id = 0,
                Titre = Titre,
                Texte = Texte,
                UrlImage = UrlImage,
                DateHistoire = parsedDate,
            };

            Histoires.Insert(0, created);
            HistoiresStatusMessage = Histoires.Count == 0
                ? "Aucune histoire chargée pour le moment."
                : $"{Histoires.Count} histoire(s) chargée(s).";
            StatusMessage = response?.Message ?? "Histoire créée avec succès.";
            ResetForm();
        }
        catch (HttpRequestException httpEx)
        {
            StatusMessage = "Impossible de créer l'histoire.";
            Debug.WriteLine($"[HISTOIRE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur inattendue lors de la création.";
            Debug.WriteLine($"[HISTOIRE] Unexpected error: {ex}");
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task UpdateHistoireAsync()
    {
        if (IsBusy || SelectedHistoire is null)
        {
            return;
        }

        if (!TryParseDate(out var parsedDate))
        {
            StatusMessage = "Renseignez un titre, un texte, une image et une date valides.";
            return;
        }

        try
        {
            IsBusy = true;
            await EnsureSessionAsync();

            var payload = new
            {
                id = SelectedHistoire.Id,
                titre = Titre?.Trim(),
                texte = Texte?.Trim(),
                urlImage = UrlImage?.Trim(),
                dateHistoire = parsedDate.ToString("O", CultureInfo.InvariantCulture),
            };

            var response = await _apis.PostAsync<object, HistoireResponse>("/histoire/update", payload);
            var updated = response?.Data;

            var targetId = SelectedHistoire.Id;
            var existing = Histoires.FirstOrDefault(h => h.Id == targetId);
            if (existing is not null)
            {
                existing.Titre = updated?.Titre ?? Titre;
                existing.Texte = updated?.Texte ?? Texte;
                existing.UrlImage = updated?.UrlImage ?? UrlImage;
                existing.DateHistoire = updated?.DateHistoire ?? parsedDate;
                SelectedHistoire = existing;
            }

            StatusMessage = response?.Message ?? "Histoire mise à jour.";
        }
        catch (HttpRequestException httpEx)
        {
            StatusMessage = "Impossible de mettre à jour cette histoire.";
            Debug.WriteLine($"[HISTOIRE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur inattendue lors de la mise à jour.";
            Debug.WriteLine($"[HISTOIRE] Unexpected error: {ex}");
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task DeleteHistoireAsync()
    {
        if (IsBusy || SelectedHistoire is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await EnsureSessionAsync();

            var payload = new { id = SelectedHistoire.Id };
            var response = await _apis.PostAsync<object, ApiMessageResponse>("/histoire/delete", payload);

            var toRemove = SelectedHistoire;
            SelectedHistoire = null;
            Histoires.Remove(toRemove);
            HistoiresStatusMessage = Histoires.Count == 0
                ? "Aucune histoire chargée pour le moment."
                : $"{Histoires.Count} histoire(s) chargée(s).";
            StatusMessage = response?.Message ?? "Histoire supprimée.";
        }
        catch (HttpRequestException httpEx)
        {
            StatusMessage = "Impossible de supprimer cette histoire.";
            Debug.WriteLine($"[HISTOIRE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur inattendue lors de la suppression.";
            Debug.WriteLine($"[HISTOIRE] Unexpected error: {ex}");
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
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

    private bool TryParseDate(out DateTime parsedDate)
    {
        parsedDate = default;

        if (string.IsNullOrWhiteSpace(Titre)
            || string.IsNullOrWhiteSpace(Texte)
            || string.IsNullOrWhiteSpace(UrlImage))
        {
            return false;
        }

        return DateTime.TryParse(DateHistoireText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsedDate);
    }

    private bool CanCreateHistoire()
    {
        return !IsBusy && !IsLoadingHistoires && TryParseDate(out _);
    }

    private bool CanUpdateHistoire()
    {
        return !IsBusy && !IsLoadingHistoires && SelectedHistoire is not null && TryParseDate(out _);
    }

    private bool CanDeleteHistoire()
    {
        return !IsBusy && !IsLoadingHistoires && SelectedHistoire is not null;
    }

    private void RefreshCommands()
    {
        (CreateHistoireCommand as Command)?.ChangeCanExecute();
        (UpdateHistoireCommand as Command)?.ChangeCanExecute();
        (DeleteHistoireCommand as Command)?.ChangeCanExecute();
        OnPropertyChanged(nameof(IsFormActionEnabled));
    }
}
