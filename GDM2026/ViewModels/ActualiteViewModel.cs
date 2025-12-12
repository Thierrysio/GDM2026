using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class ActualiteViewModel : BaseViewModel
{
    private enum ActualiteFormMode
    {
        None,
        Create,
        Update
    }

    private readonly Apis _apis;
    private readonly SessionService _sessionService = new();
    private sealed class ActualiteListResponse
    {
        [JsonProperty("data")]
        public List<Actualite>? Data { get; set; }
    }

    private bool _sessionLoaded;
    private ActualiteFormMode _currentMode;
    private string _actualiteTitle = string.Empty;
    private string _actualiteContent = string.Empty;
    private string _statusMessage = "Ajoutez une actualité avec une image pour la publier.";
    private Color _statusColor = Colors.Gold;
    private ImageSource? _selectedImage;
    private string? _selectedImageUrl;
    private string _selectedImageName = "Aucune image sélectionnée.";
    private string _selectedImageCustomName = string.Empty;
    private string _imageLibraryMessage = "Sélectionnez une image existante ou recherchez dans la bibliothèque.";
    private bool _isImageLibraryLoading;
    private AdminImage? _selectedLibraryImage;
    private bool _hasSelectedImage;
    private bool _hasLoadedActualites;
    private bool _isLoadingActualites;
    private string _actualitesStatusMessage = "Chargement des actualités…";
    private Actualite? _selectedActualiteForEdit;
    private string _imageSearchTerm = string.Empty;

    public ObservableCollection<AdminImage> ImageLibrary { get; } = new();
    public ObservableCollection<AdminImage> FilteredImageLibrary { get; } = new();
    public ObservableCollection<Actualite> Actualites { get; } = new();

    public ActualiteViewModel()
    {
        _apis = new Apis();

        ShowCreatePanelCommand = new Command(async () => await SetModeAsync(ActualiteFormMode.Create));
        ShowUpdatePanelCommand = new Command(async () => await SetModeAsync(ActualiteFormMode.Update));

        CreateActualiteCommand = new Command(async () => await CreateActualiteAsync(), CanCreateActualite);
        UpdateActualiteCommand = new Command(async () => await UpdateActualiteAsync(), CanUpdateActualite);
        DeleteActualiteCommand = new Command(async () => await DeleteActualiteAsync(), CanDeleteActualite);
    }

    public ICommand ShowCreatePanelCommand { get; }

    public ICommand ShowUpdatePanelCommand { get; }

    public ICommand CreateActualiteCommand { get; }

    public ICommand UpdateActualiteCommand { get; }

    public ICommand DeleteActualiteCommand { get; }

    public bool IsCreateMode => _currentMode == ActualiteFormMode.Create;

    public bool IsUpdateMode => _currentMode == ActualiteFormMode.Update;

    public bool IsFormSectionVisible => _currentMode == ActualiteFormMode.Create || _currentMode == ActualiteFormMode.Update;

    public bool IsFormInputEnabled => _currentMode == ActualiteFormMode.Create || _selectedActualiteForEdit is not null;

    public string FormHeader => _currentMode switch
    {
        ActualiteFormMode.Update => "Mettre à jour une actualité",
        ActualiteFormMode.Create => "Nouvelle actualité",
        _ => "Formulaire"
    };

    public string FormHelperMessage => _currentMode switch
    {
        ActualiteFormMode.Create => "Renseignez les informations de la nouvelle actualité.",
        ActualiteFormMode.Update when _selectedActualiteForEdit is null => "Sélectionnez d'abord une actualité dans la liste ci-dessous.",
        ActualiteFormMode.Update when _selectedActualiteForEdit is not null => $"Modification de l'actualité #{_selectedActualiteForEdit.Id}.",
        _ => string.Empty
    };

    public string FormActionButtonText => _currentMode == ActualiteFormMode.Update
        ? "Mettre à jour l'actualité"
        : "Ajouter une actualité";

    public ICommand FormActionCommand => _currentMode == ActualiteFormMode.Update
        ? UpdateActualiteCommand
        : CreateActualiteCommand;

    public bool IsLoadingActualites
    {
        get => _isLoadingActualites;
        set => SetProperty(ref _isLoadingActualites, value);
    }

    public bool IsImageLibraryLoading
    {
        get => _isImageLibraryLoading;
        set => SetProperty(ref _isImageLibraryLoading, value);
    }

    public string ActualitesStatusMessage
    {
        get => _actualitesStatusMessage;
        set => SetProperty(ref _actualitesStatusMessage, value);
    }

    public string ActualiteTitle
    {
        get => _actualiteTitle;
        set
        {
            if (SetProperty(ref _actualiteTitle, value))
            {
                RefreshFormCommands();
            }
        }
    }

    public string ActualiteContent
    {
        get => _actualiteContent;
        set
        {
            if (SetProperty(ref _actualiteContent, value))
            {
                RefreshFormCommands();
            }
        }
    }

    public ImageSource? SelectedImage
    {
        get => _selectedImage;
        set => SetProperty(ref _selectedImage, value);
    }

    public string SelectedImageName
    {
        get => _selectedImageName;
        set => SetProperty(ref _selectedImageName, value);
    }

    public string SelectedImageCustomName
    {
        get => _selectedImageCustomName;
        set
        {
            if (SetProperty(ref _selectedImageCustomName, value))
            {
                UpdateSelectedImageLabel();
            }
        }
    }

    public AdminImage? SelectedLibraryImage
    {
        get => _selectedLibraryImage;
        set
        {
            if (SetProperty(ref _selectedLibraryImage, value))
            {
                ApplyImageSelection(value);
            }
        }
    }

    public bool HasSelectedImage
    {
        get => _hasSelectedImage;
        set => SetProperty(ref _hasSelectedImage, value);
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
            {
                RefreshImageLibraryFilter();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Color StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    public Actualite? SelectedActualiteForEdit
    {
        get => _selectedActualiteForEdit;
        set
        {
            if (SetProperty(ref _selectedActualiteForEdit, value))
            {
                PopulateSelection(value);
                OnPropertyChanged(nameof(HasActualiteSelection));
                OnPropertyChanged(nameof(FormHelperMessage));
                OnPropertyChanged(nameof(IsFormInputEnabled));
                RefreshFormCommands();
            }
        }
    }

    public bool HasActualiteSelection => _selectedActualiteForEdit is not null;

    public async Task EnsureActualitesLoadedAsync()
    {
        await LoadActualitesAsync();
    }

    private bool CanCreateActualite()
    {
        return !IsBusy
            && _currentMode == ActualiteFormMode.Create
            && !string.IsNullOrWhiteSpace(_actualiteContent)
            && !string.IsNullOrWhiteSpace(_actualiteTitle)
            && !string.IsNullOrWhiteSpace(_selectedImageUrl);
    }

    private bool CanUpdateActualite()
    {
        return !IsBusy
            && _currentMode == ActualiteFormMode.Update
            && _selectedActualiteForEdit is not null
            && !string.IsNullOrWhiteSpace(_actualiteTitle)
            && !string.IsNullOrWhiteSpace(_actualiteContent)
            && !string.IsNullOrWhiteSpace(_selectedImageUrl);
    }

    private bool CanDeleteActualite()
    {
        return !IsBusy
            && _currentMode == ActualiteFormMode.Update
            && _selectedActualiteForEdit is not null;
    }

    private void RefreshFormCommands()
    {
        (CreateActualiteCommand as Command)?.ChangeCanExecute();
        (UpdateActualiteCommand as Command)?.ChangeCanExecute();
        (DeleteActualiteCommand as Command)?.ChangeCanExecute();
    }

    private async Task SetModeAsync(ActualiteFormMode mode)
    {
        if (_currentMode == mode)
        {
            _currentMode = ActualiteFormMode.None;
            OnModeChanged();
            SelectedActualiteForEdit = null;
            return;
        }

        _currentMode = mode;
        OnModeChanged();

        switch (mode)
        {
            case ActualiteFormMode.Create:
                PrepareCreateForm();
                await EnsureImageLibraryAsync();
                break;
            case ActualiteFormMode.Update:
                PrepareUpdateForm();
                await EnsureImageLibraryAsync();
                await LoadActualitesAsync(forceReload: true);
                break;
        }

        RefreshFormCommands();
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
        ActualiteTitle = string.Empty;
        ActualiteContent = string.Empty;
        SelectedLibraryImage = null;
        ClearSelectedImage();
    }

    private void PrepareUpdateForm()
    {
        SelectedActualiteForEdit = null;
        SelectedLibraryImage = null;
        ClearSelectedImage();
        ActualiteTitle = string.Empty;
        ActualiteContent = string.Empty;
    }

    private async Task EnsureImageLibraryAsync()
    {
        if (ImageLibrary.Count == 0 && !IsImageLibraryLoading)
        {
            await LoadImageLibraryAsync();
        }
    }

    private void PopulateSelection(Actualite? actualite)
    {
        if (actualite is null)
        {
            return;
        }

        ActualiteTitle = actualite.Titre ?? string.Empty;
        ActualiteContent = actualite.Description ?? string.Empty;

        var imagePath = actualite.PrimaryImage;
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            _selectedImageUrl = imagePath;
            HasSelectedImage = true;
            SelectedImageCustomName = actualite.Titre;
            SelectedImage = string.IsNullOrWhiteSpace(actualite.FullImageUrl)
                ? null
                : ImageSource.FromUri(new Uri(actualite.FullImageUrl));
            UpdateSelectedImageLabel();
        }
        else
        {
            ClearSelectedImage();
        }
    }

    private void ApplyImageSelection(AdminImage? image)
    {
        if (image is null)
        {
            ClearSelectedImage();
            RefreshFormCommands();
            return;
        }

        HasSelectedImage = true;
        _selectedImageUrl = image.Url;
        SelectedImageCustomName = image.DisplayName;
        SelectedImage = ImageSource.FromUri(new Uri(image.FullUrl));
        StatusMessage = "Image sélectionnée depuis la bibliothèque.";
        StatusColor = Colors.LightGreen;
        UpdateSelectedImageLabel();
        RefreshFormCommands();
    }

    private void ClearSelectedImage()
    {
        HasSelectedImage = false;
        _selectedImageUrl = null;
        SelectedImage = null;
        SelectedImageCustomName = string.Empty;
        SelectedImageName = "Aucune image sélectionnée.";
    }

    private void UpdateSelectedImageLabel()
    {
        SelectedImageName = HasSelectedImage && !string.IsNullOrWhiteSpace(_selectedImageCustomName)
            ? $"Image sélectionnée : {_selectedImageCustomName}"
            : "Aucune image sélectionnée.";
    }

    private async Task LoadImageLibraryAsync()
    {
        if (IsImageLibraryLoading)
        {
            return;
        }

        try
        {
            IsImageLibraryLoading = true;
            ImageLibraryMessage = "Chargement de la bibliothèque d'images…";

            if (!await EnsureAuthenticationAsync())
            {
                ImageLibraryMessage = "Connectez-vous pour parcourir les images du site.";
                return;
            }

            var images = await _apis.GetListAsync<AdminImage>("/api/crud/images/list");

            ImageLibrary.Clear();
            foreach (var image in images)
            {
                ImageLibrary.Add(image);
            }

            UpdateDefaultImageLibraryMessage();
            RefreshImageLibraryFilter();
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[IMAGES] HTTP error: {ex}");
            ImageLibraryMessage = "Impossible de charger la bibliothèque d'images.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[IMAGES] Error: {ex}");
            ImageLibraryMessage = "Erreur lors du chargement des images.";
        }
        finally
        {
            IsImageLibraryLoading = false;
        }
    }

    private void RefreshImageLibraryFilter()
    {
        if (IsImageLibraryLoading)
        {
            return;
        }

        var hasSearch = !string.IsNullOrWhiteSpace(_imageSearchTerm);
        var normalizedSearch = _imageSearchTerm?.Trim().ToLowerInvariant();

        var filtered = hasSearch
            ? ImageLibrary.Where(img =>
                (!string.IsNullOrWhiteSpace(img.DisplayName) && img.DisplayName.ToLowerInvariant().Contains(normalizedSearch))
                || (!string.IsNullOrWhiteSpace(img.Url) && img.Url.ToLowerInvariant().Contains(normalizedSearch)))
            : ImageLibrary.AsEnumerable();

        FilteredImageLibrary.Clear();
        foreach (var image in filtered)
        {
            FilteredImageLibrary.Add(image);
        }

        if (hasSearch)
        {
            ImageLibraryMessage = FilteredImageLibrary.Count == 0
                ? "Aucune image ne correspond à cette recherche."
                : $"Résultats pour \"{_imageSearchTerm}\" ({FilteredImageLibrary.Count}).";

            if (SelectedLibraryImage is not null && !FilteredImageLibrary.Contains(SelectedLibraryImage))
            {
                SelectedLibraryImage = null;
            }
        }
        else
        {
            UpdateDefaultImageLibraryMessage();
        }
    }

    private void UpdateDefaultImageLibraryMessage()
    {
        ImageLibraryMessage = ImageLibrary.Count == 0
            ? "Aucune image disponible dans l'admin."
            : "Sélectionnez une image existante ou recherchez dans la bibliothèque.";
    }

    private async Task LoadActualitesAsync(bool forceReload = false)
    {
        if (IsLoadingActualites || (!forceReload && _hasLoadedActualites))
        {
            return;
        }

        try
        {
            IsLoadingActualites = true;
            ActualitesStatusMessage = "Chargement des actualités…";

            var actualites = await FetchActualitesAsync();

            Actualites.Clear();
            foreach (var actualite in actualites.OrderByDescending(a => a.CreatedAt ?? DateTime.MinValue)
                                               .ThenByDescending(a => a.Id))
            {
                Actualites.Add(actualite);
            }

            ActualitesStatusMessage = Actualites.Count == 0
                ? "Aucune actualité publiée pour le moment."
                : "Actualités publiées depuis l'admin.";
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[ACTUALITES] HTTP error: {ex}");
            ActualitesStatusMessage = "Impossible de récupérer les actualités depuis l'admin.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ACTUALITES] Error: {ex}");
            ActualitesStatusMessage = "Erreur lors du chargement des actualités.";
        }
        finally
        {
            _hasLoadedActualites = true;
            IsLoadingActualites = false;
        }
    }

    private async Task CreateActualiteAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_actualiteTitle) || string.IsNullOrWhiteSpace(_actualiteContent))
        {
            StatusMessage = "Renseignez le titre et la description de l'actualité.";
            StatusColor = Colors.OrangeRed;
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedImageUrl))
        {
            StatusMessage = "Ajoutez une image avant de publier l'actualité.";
            StatusColor = Colors.OrangeRed;
            return;
        }

        try
        {
            IsBusy = true;
            RefreshFormCommands();

            StatusMessage = "Publication en cours…";
            StatusColor = Colors.Gold;

            if (!await EnsureAuthenticationAsync())
            {
                return;
            }

            var payload = new
            {
                titre = _actualiteTitle.Trim(),
                texte = _actualiteContent.Trim(),
                image = _selectedImageUrl
            };

            var created = await _apis.PostBoolAsync("/actualite/create", payload);
            StatusMessage = created
                ? "Actualité publiée avec succès."
                : "La publication a échoué. Veuillez réessayer.";
            StatusColor = created ? Colors.LightGreen : Colors.OrangeRed;

            if (created)
            {
                PrepareCreateForm();
                await LoadActualitesAsync(forceReload: true);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Publication annulée.";
            StatusColor = Colors.Gold;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Debug.WriteLine($"[ACTUALITE] Jeton refusé : {ex}");
            StatusMessage = "Authentification requise ou expirée. Reconnectez-vous pour publier.";
            StatusColor = Colors.OrangeRed;

            if (await PromptInlineLoginAsync())
            {
                ApplyAuthToken();
                StatusMessage = "Connexion rétablie. Relancez la publication.";
                StatusColor = Colors.LightGreen;
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[ACTUALITE] HTTP error: {ex}");
            StatusMessage = $"Impossible de contacter le serveur dantecmarket.com. ({ex.Message})";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Publication actualité échouée: {ex}");
            StatusMessage = $"Une erreur est survenue : {ex.Message}";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsBusy = false;
            RefreshFormCommands();
        }
    }

    private async Task UpdateActualiteAsync()
    {
        if (IsBusy || _selectedActualiteForEdit is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedImageUrl))
        {
            StatusMessage = "Ajoutez ou sélectionnez une image avant de mettre à jour.";
            StatusColor = Colors.OrangeRed;
            return;
        }

        try
        {
            IsBusy = true;
            RefreshFormCommands();

            StatusMessage = "Mise à jour en cours…";
            StatusColor = Colors.Gold;

            if (!await EnsureAuthenticationAsync())
            {
                return;
            }

            var payload = new
            {
                id = _selectedActualiteForEdit.Id,
                titre = _actualiteTitle.Trim(),
                texte = _actualiteContent.Trim(),
                image = _selectedImageUrl
            };

            var updated = await _apis.PostBoolAsync("/actualite/update", payload);
            StatusMessage = updated
                ? "Actualité mise à jour."
                : "La mise à jour a échoué.";
            StatusColor = updated ? Colors.LightGreen : Colors.OrangeRed;

            if (updated)
            {
                await LoadActualitesAsync(forceReload: true);
                PrepareUpdateForm();
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Mise à jour annulée.";
            StatusColor = Colors.Gold;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Debug.WriteLine($"[ACTUALITE] Update 401 : {ex}");
            StatusMessage = "Authentification requise pour mettre à jour.";
            StatusColor = Colors.OrangeRed;

            if (await PromptInlineLoginAsync())
            {
                ApplyAuthToken();
                StatusMessage = "Connexion rétablie. Relancez la mise à jour.";
                StatusColor = Colors.LightGreen;
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[ACTUALITE] HTTP error update: {ex}");
            StatusMessage = $"Impossible de contacter le serveur dantecmarket.com. ({ex.Message})";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Mise à jour actualité échouée: {ex}");
            StatusMessage = $"Une erreur est survenue : {ex.Message}";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsBusy = false;
            RefreshFormCommands();
        }
    }

    private async Task DeleteActualiteAsync()
    {
        if (IsBusy || _selectedActualiteForEdit is null)
        {
            return;
        }

        var shell = Shell.Current;
        if (shell is not null)
        {
            var confirmed = await shell.DisplayAlert(
                "Suppression",
                $"Supprimer définitivement l'actualité #{_selectedActualiteForEdit.Id} ?",
                "Supprimer",
                "Annuler");

            if (!confirmed)
            {
                return;
            }
        }

        try
        {
            IsBusy = true;
            RefreshFormCommands();

            StatusMessage = "Suppression en cours…";
            StatusColor = Colors.Gold;

            if (!await EnsureAuthenticationAsync())
            {
                return;
            }

            var deleted = await _apis.PostBoolAsync("/actualite/delete", new { id = _selectedActualiteForEdit.Id });
            StatusMessage = deleted
                ? "Actualité supprimée."
                : "La suppression a échoué.";
            StatusColor = deleted ? Colors.LightGreen : Colors.OrangeRed;

            if (deleted)
            {
                SelectedActualiteForEdit = null;
                PrepareUpdateForm();
                await LoadActualitesAsync(forceReload: true);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Suppression annulée.";
            StatusColor = Colors.Gold;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            StatusMessage = "Authentification requise pour supprimer.";
            StatusColor = Colors.OrangeRed;

            if (await PromptInlineLoginAsync())
            {
                ApplyAuthToken();
                StatusMessage = "Connexion rétablie. Relancez la suppression.";
                StatusColor = Colors.LightGreen;
            }
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Impossible de contacter le serveur dantecmarket.com. ({ex.Message})";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Suppression impossible : {ex.Message}";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsBusy = false;
            RefreshFormCommands();
        }
    }

    private async Task<bool> EnsureAuthenticationAsync()
    {
        if (!_sessionLoaded)
        {
            _sessionLoaded = true;
            await _sessionService.LoadAsync();
        }

        if (!string.IsNullOrWhiteSpace(_sessionService.AuthToken))
        {
            ApplyAuthToken();
            return true;
        }

        StatusMessage = "Vous devez vous reconnecter.";
        StatusColor = Colors.OrangeRed;

        if (!await PromptInlineLoginAsync())
        {
            StatusMessage = "Connexion requise pour continuer.";
            StatusColor = Colors.OrangeRed;
            return false;
        }

        ApplyAuthToken();
        return true;
    }

    private void ApplyAuthToken()
    {
        _apis.SetBearerToken(_sessionService.AuthToken);
    }

    private async Task<bool> PromptInlineLoginAsync()
    {
        var credentials = await RequestCredentialsAsync();
        if (credentials is null)
        {
            return false;
        }

        StatusMessage = "Connexion en cours…";
        StatusColor = Colors.Gold;

        try
        {
            var user = await AuthenticateAsync(credentials.Value.username, credentials.Value.password);
            if (user is null)
            {
                StatusMessage = "Identifiants invalides.";
                StatusColor = Colors.OrangeRed;
                return false;
            }

            await _sessionService.SaveAsync(user, user.Token);

            if (string.IsNullOrWhiteSpace(_sessionService.AuthToken))
            {
                StatusMessage = "Connexion effectuée mais aucun jeton reçu.";
                StatusColor = Colors.OrangeRed;
                return false;
            }

            StatusMessage = "Connexion réussie. Relancez l'action.";
            StatusColor = Colors.LightGreen;
            return true;
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "Délai d'authentification dépassé.";
            StatusColor = Colors.OrangeRed;
            return false;
        }
        catch (HttpRequestException)
        {
            StatusMessage = "Impossible de joindre le serveur pour l'authentification.";
            StatusColor = Colors.OrangeRed;
            return false;
        }
    }

    private static async Task<(string username, string password)?> RequestCredentialsAsync()
    {
        var shell = Shell.Current;
        if (shell is null)
        {
            return null;
        }

        var username = await shell.DisplayPromptAsync(
            "Connexion requise",
            "Identifiez-vous pour continuer.",
            accept: "Continuer",
            cancel: "Annuler",
            placeholder: "Email ou identifiant");

        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var password = await shell.DisplayPromptAsync(
            "Mot de passe",
            "Saisissez votre mot de passe pour continuer.",
            accept: "Valider",
            cancel: "Annuler",
            placeholder: "Mot de passe",
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return (username.Trim(), password);
    }

    private async Task<User?> AuthenticateAsync(string username, string password)
    {
        var loginData = new
        {
            Email = username,
            Password = password
        };

        try
        {
            return await _apis.PostAsync<object, User>("/api/mobile/GetFindUser", loginData);
        }
        catch (HttpRequestException ex) when (ex.Message.StartsWith("API error", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    private async Task<List<Actualite>> FetchActualitesAsync()
    {
        var response = await _apis.PostAsync<object, ActualiteListResponse>("/actualite/list", new { });
        return response?.Data ?? new List<Actualite>();
    }
}
