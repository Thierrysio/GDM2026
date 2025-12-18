using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class PartnersViewModel : BaseViewModel
{
    private enum PartnerFormMode
    {
        None,
        Create,
        Update
    }

    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionPrepared;

    private PartnerFormMode _currentMode;
    private bool _isLoading;
    private string _statusMessage = "Cliquez sur « Charger / Recharger les partenaires ».";

    private Partner? _selectedPartner;

    private string _partnerName = string.Empty;
    private string _partnerWebsite = string.Empty;

    // ====== Image library / logo ======
    private bool _imageLibraryLoaded;
    private bool _isImageLibraryLoading;
    private string _imageLibraryMessage = "Sélectionnez un logo ou utilisez la recherche.";
    private string _imageSearchTerm = string.Empty;

    private AdminImage? _selectedLibraryImage;
    private bool _hasSelectedImage;
    private string _selectedImageName = "Aucun logo sélectionné.";
    private ImageSource? _selectedImagePreview;

    // chemin/url (stockée) du logo sélectionné
    private string? _selectedImageUrlOrPath;

    public PartnersViewModel()
    {
        LoadPartnersCommand = new Command(async () => await LoadPartnersAsync(forceReload: true),
            () => !IsLoading && !IsBusy);

        ShowCreatePanelCommand = new Command(async () => await SetModeAsync(PartnerFormMode.Create));
        ShowUpdatePanelCommand = new Command(async () => await SetModeAsync(PartnerFormMode.Update));

        CreateCommand = new Command(async () => await CreateAsync(), CanCreate);
        UpdateCommand = new Command(async () => await UpdateAsync(), CanUpdate);
        DeleteCommand = new Command(async () => await DeleteAsync(), CanDelete);

        OpenWebsiteCommand = new Command<string?>(async url => await OpenWebsiteAsync(url));
        ClearLogoCommand = new Command(ClearSelectedLogo);
    }

    // appelé par la page : ne charge pas les partenaires
    public async Task OnPageAppearingAsync()
    {
        if (!_sessionPrepared)
            await EnsureSessionAsync();

        StatusMessage = "Cliquez sur « Charger / Recharger les partenaires ».";
        _ = EnsureImageLibraryLoadedAsync();
        _currentMode = PartnerFormMode.None;
        OnModeChanged();
        RefreshCommands();
    }

    /* =======================
     *  COLLECTIONS
     * ======================= */

    public ObservableCollection<Partner> Partners { get; } = new();
    public ObservableCollection<AdminImage> ImageLibrary { get; } = new();
    public ObservableCollection<AdminImage> FilteredImageLibrary { get; } = new();

    /* =======================
     *  COMMANDES
     * ======================= */

    public ICommand ShowCreatePanelCommand { get; }

    public ICommand ShowUpdatePanelCommand { get; }

    public ICommand LoadPartnersCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenWebsiteCommand { get; }
    public ICommand ClearLogoCommand { get; }

    /* =======================
     *  ETATS
     * ======================= */

    public bool IsCreateMode => _currentMode == PartnerFormMode.Create;

    public bool IsUpdateMode => _currentMode == PartnerFormMode.Update;

    public bool IsFormSectionVisible => _currentMode == PartnerFormMode.Create || _currentMode == PartnerFormMode.Update;

    public bool IsFormInputEnabled => _currentMode == PartnerFormMode.Create || SelectedPartner is not null;

    public string FormHeader => _currentMode switch
    {
        PartnerFormMode.Create => "Créer un partenaire",
        PartnerFormMode.Update => "Mettre à jour un partenaire",
        _ => "Gestion des partenaires"
    };

    public string FormHelperMessage => _currentMode switch
    {
        PartnerFormMode.Create => "Renseignez les informations du partenaire.",
        PartnerFormMode.Update when SelectedPartner is null => "Sélectionnez d'abord un partenaire ci-dessous.",
        PartnerFormMode.Update when SelectedPartner is not null => $"Modification de « {SelectedPartner.DisplayName} ».",
        _ => string.Empty
    };

    public string FormActionButtonText => _currentMode == PartnerFormMode.Update
        ? "Mettre à jour le partenaire"
        : "Créer le partenaire";

    public ICommand FormActionCommand => _currentMode == PartnerFormMode.Update
        ? UpdateCommand
        : CreateCommand;

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
                RefreshCommands();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /* =======================
     *  SELECTION PARTNER
     * ======================= */

    public Partner? SelectedPartner
    {
        get => _selectedPartner;
        set
        {
            if (SetProperty(ref _selectedPartner, value))
            {
                ApplyExistingLogo(value);
                UpdateFormFromSelection();

                OnPropertyChanged(nameof(SelectedPartnerLabel));
                OnPropertyChanged(nameof(HasPartnerSelection));
                OnPropertyChanged(nameof(IsFormInputEnabled));
                OnPropertyChanged(nameof(FormHelperMessage));
                OnPropertyChanged(nameof(FormActionCommand));
                RefreshCommands();
            }
        }
    }

    public string SelectedPartnerLabel =>
        SelectedPartner is null
            ? "Aucun partenaire sélectionné."
            : $"#{SelectedPartner.Id} — {SelectedPartner.DisplayName}";

    public bool HasPartnerSelection => SelectedPartner is not null;

    /* =======================
     *  CHAMPS FORMULAIRE
     * ======================= */

    public string PartnerName
    {
        get => _partnerName;
        set
        {
            if (SetProperty(ref _partnerName, value))
                RefreshCommands();
        }
    }

    public string PartnerWebsite
    {
        get => _partnerWebsite;
        set => SetProperty(ref _partnerWebsite, value);
    }

    private bool CanCreate() =>
        !IsBusy && !IsLoading && IsCreateMode && !string.IsNullOrWhiteSpace(PartnerName);

    private async Task CreateAsync()
    {
        if (!CanCreate())
        {
            StatusMessage = "Renseignez le nom du partenaire.";
            return;
        }

        try
        {
            IsBusy = true;
            IsLoading = true;
            RefreshCommands();

            await EnsureSessionAsync();

            var payload = new
            {
                nom = PartnerName.Trim(),
                url = string.IsNullOrWhiteSpace(PartnerWebsite) ? null : PartnerWebsite.Trim(),
                // logo : envoie ce que tu veux côté API (chemin relatif conseillé)
                logo = _selectedImageUrlOrPath
            };

            var ok = await _apis.PostBoolAsync("/api/crud/partenaires/create", payload);

            StatusMessage = ok ? "Partenaire créé." : "La création a échoué.";

            if (ok)
            {
                PartnerName = string.Empty;
                PartnerWebsite = string.Empty;
                ClearSelectedLogo();
                await LoadPartnersAsync(forceReload: true);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de créer le partenaire.";
            Debug.WriteLine($"[PARTNERS] create error: {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    /* =======================
     *  CHAMPS EDIT
     * ======================= */

    private bool CanUpdate() =>
        !IsBusy && !IsLoading && IsUpdateMode && SelectedPartner is not null && !string.IsNullOrWhiteSpace(PartnerName);

    private async Task UpdateAsync()
    {
        if (!CanUpdate())
        {
            StatusMessage = "Sélectionnez un partenaire et renseignez au moins son nom.";
            return;
        }

        var partner = SelectedPartner!;
        var confirm = await ConfirmAsync("Mise à jour", $"Mettre à jour « {partner.DisplayName} » ?");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            IsLoading = true;
            RefreshCommands();

            await EnsureSessionAsync();

            var payload = new
            {
                id = partner.Id,
                nom = PartnerName.Trim(),
                url = string.IsNullOrWhiteSpace(PartnerWebsite) ? null : PartnerWebsite.Trim(),
                logo = _selectedImageUrlOrPath ?? partner.ImagePath
            };

            var ok = await _apis.PostBoolAsync("/api/crud/partenaires/update", payload);

            StatusMessage = ok ? "Partenaire mis à jour." : "La mise à jour a échoué.";

            if (ok)
                await LoadPartnersAsync(forceReload: true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de mettre à jour le partenaire.";
            Debug.WriteLine($"[PARTNERS] update error: {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    private bool CanDelete() =>
        !IsBusy && !IsLoading && IsUpdateMode && SelectedPartner is not null;

    private async Task DeleteAsync()
    {
        if (!CanDelete())
        {
            StatusMessage = "Sélectionnez un partenaire à supprimer.";
            return;
        }

        var partner = SelectedPartner!;
        var confirm = await ConfirmAsync("Suppression", $"Supprimer « {partner.DisplayName} » ?");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            IsLoading = true;
            RefreshCommands();

            await EnsureSessionAsync();

            var ok = await _apis.PostBoolAsync("/api/crud/partenaires/delete", new { id = partner.Id });

            if (!ok)
            {
                StatusMessage = "La suppression a échoué.";
                return;
            }

            Partners.Remove(partner);
            SelectedPartner = null;
            ClearSelectedLogo();
            StatusMessage = "Partenaire supprimé.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de supprimer le partenaire.";
            Debug.WriteLine($"[PARTNERS] delete error: {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    private async Task SetModeAsync(PartnerFormMode mode)
    {
        if (_currentMode == mode)
        {
            _currentMode = PartnerFormMode.None;
            OnModeChanged();
            SelectedPartner = null;
            PartnerName = string.Empty;
            PartnerWebsite = string.Empty;
            ClearSelectedLogo();
            RefreshCommands();
            return;
        }

        _currentMode = mode;
        OnModeChanged();

        switch (mode)
        {
            case PartnerFormMode.Create:
                PrepareCreateForm();
                await EnsureImageLibraryLoadedAsync();
                break;
            case PartnerFormMode.Update:
                PrepareUpdateForm();
                await EnsureImageLibraryLoadedAsync();
                await LoadPartnersAsync(forceReload: true);
                break;
        }

        RefreshCommands();
    }

    private void OnModeChanged()
    {
        OnPropertyChanged(nameof(IsCreateMode));
        OnPropertyChanged(nameof(IsUpdateMode));
        OnPropertyChanged(nameof(IsFormSectionVisible));
        OnPropertyChanged(nameof(IsFormInputEnabled));
        OnPropertyChanged(nameof(FormHeader));
        OnPropertyChanged(nameof(FormHelperMessage));
        OnPropertyChanged(nameof(FormActionButtonText));
        OnPropertyChanged(nameof(FormActionCommand));
    }

    private void PrepareCreateForm()
    {
        SelectedPartner = null;
        PartnerName = string.Empty;
        PartnerWebsite = string.Empty;
        SelectedLibraryImage = null;
        ClearSelectedLogo();
    }

    private void PrepareUpdateForm()
    {
        SelectedPartner = null;
        PartnerName = string.Empty;
        PartnerWebsite = string.Empty;
        SelectedLibraryImage = null;
        ClearSelectedLogo();
    }

    /* =======================
     *  LOAD PARTNERS
     * ======================= */

    private async Task LoadPartnersAsync(bool forceReload)
    {
        if (IsBusy || IsLoading)
            return;

        try
        {
            IsBusy = true;
            IsLoading = true;

            StatusMessage = "Chargement des partenaires…";

            await EnsureSessionAsync();

            var list = await _apis.GetListAsync<Partner>("/api/crud/partenaires/list");
            list ??= new List<Partner>();

            Partners.Clear();
            foreach (var p in list)
                Partners.Add(p);

            StatusMessage = Partners.Count == 0
                ? "Aucun partenaire."
                : $"{Partners.Count} partenaire(s) chargé(s).";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            StatusMessage = "Accès refusé. Reconnectez-vous.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur lors du chargement.";
            Debug.WriteLine($"[PARTNERS] load error: {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    /* =======================
     *  IMAGE LIBRARY
     * ======================= */

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
                RefreshImageLibraryFilter();
        }
    }

    public AdminImage? SelectedLibraryImage
    {
        get => _selectedLibraryImage;
        set
        {
            if (SetProperty(ref _selectedLibraryImage, value))
                ApplyImageSelection(value);
        }
    }

    public bool HasSelectedImage
    {
        get => _hasSelectedImage;
        set => SetProperty(ref _hasSelectedImage, value);
    }

    public string SelectedImageName
    {
        get => _selectedImageName;
        set => SetProperty(ref _selectedImageName, value);
    }

    public ImageSource? SelectedImagePreview
    {
        get => _selectedImagePreview;
        set => SetProperty(ref _selectedImagePreview, value);
    }

    private async Task EnsureImageLibraryLoadedAsync()
    {
        if (_imageLibraryLoaded || IsImageLibraryLoading)
            return;

        try
        {
            IsImageLibraryLoading = true;
            ImageLibraryMessage = "Chargement de la bibliothèque d'images…";

            await EnsureSessionAsync();

            var images = await _apis.GetListAsync<AdminImage>("/api/crud/images/list");
            images ??= new List<AdminImage>();

            ImageLibrary.Clear();
            foreach (var img in images)
                ImageLibrary.Add(img);

            _imageLibraryLoaded = true;

            RefreshImageLibraryFilter();
        }
        catch (Exception ex)
        {
            ImageLibraryMessage = "Impossible de charger la bibliothèque d'images.";
            Debug.WriteLine($"[PARTNERS] image library error: {ex}");
        }
        finally
        {
            IsImageLibraryLoading = false;
        }
    }

    // ✅ FILTRE : robuste et immédiat
    private void RefreshImageLibraryFilter()
    {
        if (IsImageLibraryLoading)
            return;

        FilteredImageLibrary.Clear();

        IEnumerable<AdminImage> source = ImageLibrary;

        if (!string.IsNullOrWhiteSpace(ImageSearchTerm))
        {
            var query = ImageSearchTerm.Trim().ToLowerInvariant();
            source = source.Where(img =>
                !string.IsNullOrWhiteSpace(img.DisplayName) &&
                img.DisplayName.ToLowerInvariant().Contains(query));
        }

        foreach (var img in source)
            FilteredImageLibrary.Add(img);

        ImageLibraryMessage = FilteredImageLibrary.Count switch
        {
            0 when string.IsNullOrWhiteSpace(ImageSearchTerm) => "Aucune image disponible.",
            0 => $"Aucun résultat pour « {ImageSearchTerm} ».",
            _ when string.IsNullOrWhiteSpace(ImageSearchTerm) => "Sélectionnez un logo ou utilisez la recherche.",
            _ => $"{FilteredImageLibrary.Count} résultat(s) pour « {ImageSearchTerm} »."
        };
    }

    private void UpdateFormFromSelection()
    {
        if (IsUpdateMode && SelectedPartner is not null)
        {
            PartnerName = SelectedPartner.DisplayName;
            PartnerWebsite = SelectedPartner.Website ?? string.Empty;
        }
        else if (IsUpdateMode)
        {
            PartnerName = string.Empty;
            PartnerWebsite = string.Empty;
            ClearSelectedLogo();
        }
    }

    // ✅ Sélection d'une image : met à jour preview + valeur envoyée à l'API
    private void ApplyImageSelection(AdminImage? image)
    {
        if (image is null)
        {
            ClearSelectedLogo();
            return;
        }

        try
        {
            _selectedImageUrlOrPath = image.Url;

            HasSelectedImage = true;
            SelectedImageName = $"Logo sélectionné : {image.DisplayName}";
            SelectedImagePreview = ImageSource.FromUri(new Uri(image.FullUrl));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PARTNERS] selected image invalid: {ex}");
            ClearSelectedLogo();
        }
    }

    // ✅ Quand on sélectionne un partenaire, on affiche son logo
    private void ApplyExistingLogo(Partner? partner)
    {
        if (partner is null || string.IsNullOrWhiteSpace(partner.FullImageUrl))
        {
            ClearSelectedLogo();
            return;
        }

        try
        {
            _selectedImageUrlOrPath = partner.ImagePath;

            HasSelectedImage = true;
            SelectedImageName = $"Logo actuel : {partner.DisplayName}";
            SelectedImagePreview = ImageSource.FromUri(new Uri(partner.FullImageUrl));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PARTNERS] partner logo invalid: {ex}");
            ClearSelectedLogo();
        }
    }

    private void ClearSelectedLogo()
    {
        _selectedImageUrlOrPath = null;

        HasSelectedImage = false;
        SelectedImageName = "Aucun logo sélectionné.";
        SelectedImagePreview = null;

        _selectedLibraryImage = null;
        OnPropertyChanged(nameof(SelectedLibraryImage));
    }

    /* =======================
     *  SESSION / NAV
     * ======================= */

    private async Task EnsureSessionAsync()
    {
        if (_sessionPrepared)
            return;

        await _sessionService.LoadAsync();
        _apis.SetBearerToken(_sessionService.AuthToken);
        _sessionPrepared = true;
    }

    private static async Task OpenWebsiteAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var trimmed = url.Trim();
        if (!trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            trimmed = $"https://{trimmed}";

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            await Launcher.OpenAsync(uri);
    }

    private void RefreshCommands()
    {
        (LoadPartnersCommand as Command)?.ChangeCanExecute();
        (CreateCommand as Command)?.ChangeCanExecute();
        (UpdateCommand as Command)?.ChangeCanExecute();
        (DeleteCommand as Command)?.ChangeCanExecute();
    }

    private Task<bool> ConfirmAsync(string title, string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null) return true;
            return await page.DisplayAlert(title, message, "Oui", "Non");
        });
    }
}
