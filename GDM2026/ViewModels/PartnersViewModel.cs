using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
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
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private const string DefaultPartnerLogo = "12ca34b82310d78aa6c989b705fe92dda310b8a4.jpg";

    private bool _sessionPrepared;
    private bool _hasLoaded;

    private bool _isLoading;
    private string _statusMessage = "Cliquez sur « Modifier / Supprimer » pour charger les partenaires.";

    private bool _isSelectionVisible;

    private Partner? _selectedPartner;

    private bool _imageLibraryLoaded;
    private bool _isImageLibraryLoading;
    private string _imageLibraryMessage = "Sélectionnez un logo ou utilisez la recherche.";
    private string _imageSearchTerm = string.Empty;
    private AdminImage? _selectedLibraryImage;
    private bool _hasSelectedImage;
    private string _selectedImageName = "Aucun logo sélectionné.";
    private ImageSource? _selectedImagePreview;
    private string? _selectedImageUrl;

    private string _editPartnerName = string.Empty;
    private string _editPartnerWebsite = string.Empty;

    private string _newPartnerName = string.Empty;
    private string _newPartnerWebsite = string.Empty;

    public PartnersViewModel()
    {
        ToggleEditModeCommand = new Command(async () => await ToggleEditModeAsync(), () => !IsLoading && !IsBusy);

        CreateCommand = new Command(async () => await CreateAsync(), CanCreate);
        UpdateCommand = new Command(async () => await UpdateAsync(), CanUpdate);
        DeleteCommand = new Command(async () => await DeleteAsync(), CanDelete);

        OpenWebsiteCommand = new Command<string?>(async url => await OpenWebsiteAsync(url));
        ClearLogoCommand = new Command(ClearSelectedLogo);
    }

    public ObservableCollection<Partner> Partners { get; } = new();
    public ObservableCollection<AdminImage> ImageLibrary { get; } = new();
    public ObservableCollection<AdminImage> FilteredImageLibrary { get; } = new();

    public ICommand ToggleEditModeCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenWebsiteCommand { get; }
    public ICommand ClearLogoCommand { get; }

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

    public bool IsSelectionVisible
    {
        get => _isSelectionVisible;
        set
        {
            if (SetProperty(ref _isSelectionVisible, value))
            {
                OnPropertyChanged(nameof(EditModeButtonText));
                RefreshCommands();
            }
        }
    }

    public string EditModeButtonText => IsSelectionVisible ? "Masquer" : "Modifier / Supprimer";

    public Partner? SelectedPartner
    {
        get => _selectedPartner;
        set
        {
            if (SetProperty(ref _selectedPartner, value))
            {
                EditPartnerName = value?.DisplayName ?? string.Empty;
                EditPartnerWebsite = value?.Website ?? string.Empty;
                ApplyExistingLogo(value);
                OnPropertyChanged(nameof(SelectedPartnerLabel));
                RefreshCommands();
            }
        }
    }

    public string SelectedPartnerLabel => SelectedPartner is null
        ? "Aucun partenaire sélectionné."
        : $"#{SelectedPartner.Id} — {SelectedPartner.DisplayName}";

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

    public string EditPartnerName
    {
        get => _editPartnerName;
        set
        {
            if (SetProperty(ref _editPartnerName, value))
                RefreshCommands();
        }
    }

    public string EditPartnerWebsite
    {
        get => _editPartnerWebsite;
        set
        {
            if (SetProperty(ref _editPartnerWebsite, value))
                RefreshCommands();
        }
    }

    public string NewPartnerName
    {
        get => _newPartnerName;
        set
        {
            if (SetProperty(ref _newPartnerName, value))
                RefreshCommands();
        }
    }

    public string NewPartnerWebsite
    {
        get => _newPartnerWebsite;
        set
        {
            if (SetProperty(ref _newPartnerWebsite, value))
                RefreshCommands();
        }
    }

    // appelé par la page : NE charge rien
    public async Task OnPageAppearingAsync()
    {
        if (!_sessionPrepared)
            await PrepareSessionAsync();

        StatusMessage = "Cliquez sur « Modifier / Supprimer » pour charger les partenaires.";
        _ = EnsureImageLibraryLoadedAsync();
    }

    private async Task PrepareSessionAsync()
    {
        try
        {
            await _sessionService.LoadAsync();
            _apis.SetBearerToken(_sessionService.AuthToken);
            _sessionPrepared = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PARTNERS] Session non préparée : {ex}");
            _sessionPrepared = true;
        }
    }

    private async Task ToggleEditModeAsync()
    {
        IsSelectionVisible = !IsSelectionVisible;

        if (IsSelectionVisible)
        {
            await EnsureImageLibraryLoadedAsync();
            await LoadPartnersAsync(forceReload: true);
        }
        else
        {
            SelectedPartner = null;
            EditPartnerName = string.Empty;
            EditPartnerWebsite = string.Empty;
        }
    }

    private async Task LoadPartnersAsync(bool forceReload = false)
    {
        if (IsBusy || IsLoading)
            return;

        if (!forceReload && _hasLoaded)
            return;

        try
        {
            IsLoading = true;
            IsBusy = true;

            StatusMessage = "Chargement des partenaires…";

            if (!_sessionPrepared)
                await PrepareSessionAsync();

            var partners = await _apis.GetListAsync<Partner>("/api/crud/partenaires/list");
            partners ??= new List<Partner>();

            Partners.Clear();
            foreach (var partner in partners)
                Partners.Add(partner);

            _hasLoaded = true;

            StatusMessage = Partners.Count == 0
                ? "Aucun partenaire à afficher."
                : $"{Partners.Count} partenaire(s) chargé(s).";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            StatusMessage = "Accès refusé. Veuillez vous reconnecter.";
            Debug.WriteLine($"[PARTNERS] 401 : {ex}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de récupérer les partenaires.";
            Debug.WriteLine($"[PARTNERS] load error : {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    private bool CanCreate()
        => !IsBusy
           && !IsLoading
           && !string.IsNullOrWhiteSpace(NewPartnerName);

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

            if (!_sessionPrepared)
                await PrepareSessionAsync();

            var payload = new
            {
                nom = NewPartnerName.Trim(),
                url = string.IsNullOrWhiteSpace(NewPartnerWebsite) ? null : NewPartnerWebsite.Trim(),
                logo = ResolveLogoPath(_selectedImageUrl)
            };

            var ok = await _apis.PostBoolAsync("/api/crud/partenaires/create", payload);
            // pas de ConfigureAwait(false)
            StatusMessage = ok ? "Partenaire créé." : "Création échouée.";

            NewPartnerName = string.Empty;
            NewPartnerWebsite = string.Empty;
            ClearSelectedLogo();

            // si on est en mode édition, recharge la liste
            if (IsSelectionVisible)
                await LoadPartnersAsync(forceReload: true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de créer le partenaire.";
            Debug.WriteLine($"[PARTNERS] create error : {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    private bool CanUpdate()
        => !IsBusy
           && !IsLoading
           && IsSelectionVisible
           && SelectedPartner is not null
           && !string.IsNullOrWhiteSpace(EditPartnerName);

    private async Task UpdateAsync()
    {
        if (!CanUpdate())
        {
            StatusMessage = "Sélectionnez un partenaire et renseignez au moins son nom.";
            return;
        }

        var partner = SelectedPartner!;
        var name = EditPartnerName.Trim();
        var website = string.IsNullOrWhiteSpace(EditPartnerWebsite) ? null : EditPartnerWebsite.Trim();

        var confirm = await ConfirmAsync("Mise à jour", $"Mettre à jour « {partner.DisplayName} » ?");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            IsLoading = true;
            RefreshCommands();

            if (!_sessionPrepared)
                await PrepareSessionAsync();

            var payload = new
            {
                id = partner.Id,
                nom = name,
                url = website,
                site_web = website,
                logo = ResolveLogoPath(_selectedImageUrl ?? partner.ImagePath)
            };

            // Rester sur le thread UI pour éviter les exceptions lors des mises à jour liées à la CollectionView
            var ok = await _apis.PostBoolAsync("/api/crud/partenaires/update", payload);
            if (!ok)
            {
                StatusMessage = "La mise à jour a échoué.";
                return;
            }

            StatusMessage = "Partenaire mis à jour.";
            await ShowInfoAsync("Mise à jour", "Partenaire mis à jour avec succès.");

            await LoadPartnersAsync(forceReload: true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de mettre à jour le partenaire.";
            Debug.WriteLine($"[PARTNERS] update error : {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    private async Task EnsureImageLibraryLoadedAsync()
    {
        if (_imageLibraryLoaded || IsImageLibraryLoading)
            return;

        await LoadImageLibraryAsync();
    }

    private async Task LoadImageLibraryAsync()
    {
        if (IsImageLibraryLoading)
            return;

        try
        {
            IsImageLibraryLoading = true;
            ImageLibraryMessage = "Chargement de la bibliothèque d'images…";

            if (!_sessionPrepared)
                await PrepareSessionAsync();

            var images = await _apis.GetListAsync<AdminImage>("/api/crud/images/list");

            ImageLibrary.Clear();
            foreach (var image in images ?? Enumerable.Empty<AdminImage>())
                ImageLibrary.Add(image);

            _imageLibraryLoaded = true;

            ImageLibraryMessage = ImageLibrary.Count == 0
                ? "Aucune image trouvée dans la bibliothèque."
                : "Sélectionnez un logo ou utilisez la recherche.";

            RefreshImageLibraryFilter();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ImageLibraryMessage = "Accès refusé. Reconnectez-vous pour charger les logos.";
            Debug.WriteLine($"[PARTNERS] images 401 : {ex}");
        }
        catch (Exception ex)
        {
            ImageLibraryMessage = "Impossible de charger la bibliothèque d'images.";
            Debug.WriteLine($"[PARTNERS] image load error : {ex}");
        }
        finally
        {
            IsImageLibraryLoading = false;
        }
    }

    private void RefreshImageLibraryFilter()
    {
        if (IsImageLibraryLoading)
            return;

        IEnumerable<AdminImage> source = ImageLibrary;

        if (!string.IsNullOrWhiteSpace(ImageSearchTerm))
        {
            var query = ImageSearchTerm.Trim().ToLowerInvariant();
            source = source.Where(img => img.DisplayName.ToLowerInvariant().Contains(query));
        }

        FilteredImageLibrary.Clear();
        foreach (var image in source)
            FilteredImageLibrary.Add(image);

        if (FilteredImageLibrary.Count == 0)
        {
            ImageLibraryMessage = string.IsNullOrWhiteSpace(ImageSearchTerm)
                ? "Aucune image disponible."
                : $"Aucun résultat pour \"{ImageSearchTerm}\".";
        }
        else
        {
            ImageLibraryMessage = string.IsNullOrWhiteSpace(ImageSearchTerm)
                ? "Sélectionnez un logo ou utilisez la recherche."
                : $"{FilteredImageLibrary.Count} résultat(s) pour \"{ImageSearchTerm}\".";
        }
    }

    private void ApplyImageSelection(AdminImage? image)
    {
        if (image is null)
        {
            ClearSelectedLogo();
            RefreshCommands();
            return;
        }

        _selectedImageUrl = image.Url;
        HasSelectedImage = true;
        SelectedImageName = $"Logo sélectionné : {image.DisplayName}";
        SelectedImagePreview = ImageSource.FromUri(new Uri(image.FullUrl));
        StatusMessage = "Logo sélectionné depuis la bibliothèque.";
        RefreshCommands();
    }

    private void ApplyExistingLogo(Partner? partner)
    {
        if (partner is null)
        {
            ClearSelectedLogo();
            return;
        }

        _selectedImageUrl = partner.ImagePath;
        HasSelectedImage = !string.IsNullOrWhiteSpace(partner.ImagePath);
        SelectedImageName = HasSelectedImage
            ? $"Logo actuel : {partner.ImagePath}"
            : "Aucun logo sélectionné.";
        SelectedImagePreview = string.IsNullOrWhiteSpace(partner.FullImageUrl)
            ? null
            : ImageSource.FromUri(new Uri(partner.FullImageUrl));
    }

    private void ClearSelectedLogo()
    {
        _selectedImageUrl = null;
        HasSelectedImage = false;
        SelectedImageName = "Aucun logo sélectionné.";
        SelectedImagePreview = null;
        _selectedLibraryImage = null;
        OnPropertyChanged(nameof(SelectedLibraryImage));
    }

    private static string ResolveLogoPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return $"images/{DefaultPartnerLogo}";

        var sanitized = path.Replace("\\", "/").Trim();
        sanitized = sanitized.TrimStart('/');

        if (!sanitized.Contains('/'))
            sanitized = $"images/{sanitized}";

        return sanitized;
    }

    private bool CanDelete()
        => !IsBusy
           && !IsLoading
           && IsSelectionVisible
           && SelectedPartner is not null;

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

            if (!_sessionPrepared)
                await PrepareSessionAsync();

            var payload = new { id = partner.Id };

            // Rester sur le thread UI pour éviter les exceptions lors des mises à jour liées à la CollectionView
            var ok = await _apis.PostBoolAsync("/api/crud/partenaires/delete", payload);
            if (!ok)
            {
                StatusMessage = "La suppression a échoué.";
                return;
            }

            Partners.Remove(partner);

            SelectedPartner = null;
            EditPartnerName = string.Empty;
            EditPartnerWebsite = string.Empty;

            StatusMessage = "Partenaire supprimé.";
            await ShowInfoAsync("Suppression", "Partenaire supprimé avec succès.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de supprimer le partenaire.";
            Debug.WriteLine($"[PARTNERS] delete error : {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    private async Task OpenWebsiteAsync(string? url)
    {
        var trimmed = url?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            StatusMessage = "Aucun site web n'est renseigné pour ce partenaire.";
            return;
        }

        if (!trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            trimmed = $"https://{trimmed}";

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            StatusMessage = "URL de partenaire invalide.";
            return;
        }

        try
        {
            await Launcher.OpenAsync(uri);
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible d'ouvrir le site du partenaire.";
            Debug.WriteLine($"[PARTNERS] open website error : {ex}");
        }
    }

    private void RefreshCommands()
    {
        (ToggleEditModeCommand as Command)?.ChangeCanExecute();
        (CreateCommand as Command)?.ChangeCanExecute();
        (UpdateCommand as Command)?.ChangeCanExecute();
        (DeleteCommand as Command)?.ChangeCanExecute();
    }

    private Task<bool> ConfirmAsync(string title, string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null)
                return true;

            return await page.DisplayAlert(title, message, "Oui", "Non");
        });
    }

    private Task ShowInfoAsync(string title, string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null)
                return;

            await page.DisplayAlert(title, message, "OK");
        });
    }
}
