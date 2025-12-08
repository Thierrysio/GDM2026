using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class ActualiteViewModel : BaseViewModel
{
    private readonly Apis _apis;
    private readonly SessionService _sessionService = new();

    private bool _sessionLoaded;
    private bool _isFormVisible;
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

    public ObservableCollection<AdminImage> ImageLibrary { get; } = new();
    public ObservableCollection<AdminImage> FilteredImageLibrary { get; } = new();
    public ObservableCollection<Actualite> Actualites { get; } = new();

    public ActualiteViewModel()
    {
        _apis = new Apis();

        ToggleFormCommand = new Command(async () => await ToggleFormAsync());
        SubmitCommand = new Command(async () => await SubmitAsync(), CanSubmit);
    }

    public ICommand ToggleFormCommand { get; }

    public ICommand SubmitCommand { get; }

    public bool IsFormVisible
    {
        get => _isFormVisible;
        set => SetProperty(ref _isFormVisible, value);
    }

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
                RefreshSubmitAvailability();
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
                RefreshSubmitAvailability();
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

    private string _imageSearchTerm = string.Empty;

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

    public async Task EnsureActualitesLoadedAsync()
    {
        if (_hasLoadedActualites || IsLoadingActualites)
        {
            return;
        }

        await LoadActualitesAsync();
    }

    private bool CanSubmit()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(_actualiteContent)
            && !string.IsNullOrWhiteSpace(_actualiteTitle)
            && !string.IsNullOrWhiteSpace(_selectedImageUrl);
    }

    private void RefreshSubmitAvailability()
    {
        (SubmitCommand as Command)?.ChangeCanExecute();
    }

    private async Task ToggleFormAsync()
    {
        IsFormVisible = !IsFormVisible;

        if (IsFormVisible && ImageLibrary.Count == 0)
        {
            await LoadImageLibraryAsync();
        }
    }

    private void ApplyImageSelection(AdminImage? image)
    {
        if (image is null)
        {
            HasSelectedImage = false;
            _selectedImageUrl = null;
            SelectedImage = null;
            SelectedImageCustomName = string.Empty;
            SelectedImageName = "Aucune image sélectionnée.";
            RefreshSubmitAvailability();
            return;
        }

        HasSelectedImage = true;
        _selectedImageUrl = image.Url;
        SelectedImageCustomName = image.DisplayName;
        SelectedImage = ImageSource.FromUri(new Uri(image.FullUrl));
        StatusMessage = "Image sélectionnée depuis la bibliothèque.";
        StatusColor = Colors.LightGreen;
        UpdateSelectedImageLabel();
        RefreshSubmitAvailability();
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

    private async Task LoadActualitesAsync()
    {
        try
        {
            IsLoadingActualites = true;
            ActualitesStatusMessage = "Chargement des actualités…";

            var actualites = await _apis.GetListAsync<Actualite>("/api/crud/actualite/list");

            Actualites.Clear();
            foreach (var actualite in actualites.OrderByDescending(a => a.CreatedAt ?? DateTime.MinValue))
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

    private async Task SubmitAsync()
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
            RefreshSubmitAvailability();

            StatusMessage = "Publication en cours…";
            StatusColor = Colors.Gold;

            if (!await EnsureAuthenticationAsync())
            {
                return;
            }

            var creationPayload = new
            {
                titre = _actualiteTitle.Trim(),
                description = _actualiteContent.Trim(),
                image = _selectedImageUrl
            };

            var created = await _apis.PostBoolAsync("/api/crud/actualite/create", creationPayload);
            StatusMessage = created
                ? "Actualité publiée avec succès."
                : "La publication a échoué. Veuillez réessayer.";
            StatusColor = created ? Colors.LightGreen : Colors.OrangeRed;

            if (created)
            {
                ActualiteTitle = string.Empty;
                ActualiteContent = string.Empty;
                SelectedImage = null;
                SelectedImageCustomName = string.Empty;
                SelectedImageName = "Aucune image sélectionnée.";
                _selectedImageUrl = null;
                HasSelectedImage = false;
                IsFormVisible = false;

                _hasLoadedActualites = false;
                await LoadActualitesAsync();
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
            RefreshSubmitAvailability();
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

        StatusMessage = "Vous devez vous reconnecter pour publier.";
        StatusColor = Colors.OrangeRed;

        if (!await PromptInlineLoginAsync())
        {
            StatusMessage = "Connexion requise pour publier.";
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
                StatusMessage = "Identifiants invalides. Merci de réessayer.";
                StatusColor = Colors.OrangeRed;
                return false;
            }

            await _sessionService.SaveAsync(user, user.Token);

            if (string.IsNullOrWhiteSpace(_sessionService.AuthToken))
            {
                StatusMessage = "Connexion effectuée, mais aucun jeton n'a été reçu.";
                StatusColor = Colors.OrangeRed;
                return false;
            }

            StatusMessage = "Connexion réussie. Relancez la publication.";
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
            "Identifiez-vous pour publier une actualité.",
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

}
