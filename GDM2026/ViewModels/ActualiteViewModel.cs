using GDM2026.Models;
using GDM2026.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
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
    private string _statusMessage = "Renseignez le titre et la description. L'image est facultative.";
    private Color _statusColor = Colors.Gold;
    private ImageSource? _selectedImage;
    private string _selectedImageName = "Aucune image sélectionnée.";
    private bool _imageLibraryLoaded;
    private bool _isImageLibraryLoading;
    private AdminImage? _selectedAdminImage;

    public ActualiteViewModel()
    {
        _apis = new Apis();

        ToggleFormCommand = new Command(async () => await ToggleFormAsync());
        SubmitCommand = new Command(async () => await SubmitAsync(), CanSubmit);
    }

    public ICommand ToggleFormCommand { get; }

    public ICommand SubmitCommand { get; }

    public ObservableCollection<AdminImage> AvailableImages { get; } = new();

    public bool IsFormVisible
    {
        get => _isFormVisible;
        set => SetProperty(ref _isFormVisible, value);
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

    public AdminImage? SelectedAdminImage
    {
        get => _selectedAdminImage;
        set
        {
            if (SetProperty(ref _selectedAdminImage, value))
            {
                SelectedImage = value?.Thumbnail;
                SelectedImageName = value?.DisplayName ?? "Aucune image sélectionnée.";
                RefreshSubmitAvailability();
            }
        }
    }

    public bool IsImageLibraryLoading
    {
        get => _isImageLibraryLoading;
        set => SetProperty(ref _isImageLibraryLoading, value);
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

    private bool CanSubmit()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(_actualiteContent)
            && !string.IsNullOrWhiteSpace(_actualiteTitle);
    }

    private void RefreshSubmitAvailability()
    {
        (SubmitCommand as Command)?.ChangeCanExecute();
    }

    private async Task ToggleFormAsync()
    {
        IsFormVisible = !IsFormVisible;

        if (IsFormVisible)
        {
            await EnsureImageLibraryLoadedAsync();
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
                image = _selectedAdminImage?.Url
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
                SelectedImageName = "Aucune image sélectionnée.";
                SelectedAdminImage = null;
                IsFormVisible = false;
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

    private async Task EnsureImageLibraryLoadedAsync()
    {
        if (_imageLibraryLoaded || IsImageLibraryLoading)
        {
            return;
        }

        try
        {
            IsImageLibraryLoading = true;
            StatusMessage = "Chargement de la bibliothèque d'images…";
            StatusColor = Colors.Gold;

            var response = await _apis.GetListAsync<JObject>("/api/crud/images/list");
            var baseUri = _apis.HttpClient.BaseAddress ?? AppHttpClientFactory.GetValidatedBaseAddress();

            var filtered = response
                .Select(ToAdminImage)
                .Where(img => img is not null)
                .Select(img => AttachSource(img!, baseUri))
                .ToList();

            AvailableImages.Clear();
            foreach (var img in filtered)
            {
                AvailableImages.Add(img);
            }

            _imageLibraryLoaded = true;
            StatusMessage = AvailableImages.Any()
                ? "Bibliothèque d'images admin chargée (sélection facultative)."
                : "Aucune image trouvée dans la catégorie 'image'.";
            StatusColor = Colors.LightGreen;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ACTUALITE] Chargement bibliothèque images échoué: {ex}");
            StatusMessage = $"Impossible de charger la bibliothèque d'images : {ex.Message}";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsImageLibraryLoading = false;
        }
    }

    private static AdminImage AttachSource(AdminImage image, Uri? baseUri)
    {
        if (Uri.TryCreate(image.Url, UriKind.Absolute, out var absolute))
        {
            image.ImageUri = absolute;
        }
        else if (baseUri != null)
        {
            var path = image.Url.StartsWith("/") ? image.Url : "/" + image.Url;
            image.ImageUri = new Uri(baseUri, path);
        }

        image.Thumbnail = image.ImageUri != null ? ImageSource.FromUri(image.ImageUri) : null;
        image.DisplayName ??= Path.GetFileName(image.Url);

        return image;
    }

    private static AdminImage? ToAdminImage(JObject source)
    {
        var url = source.Value<string>("url")
                  ?? source.Value<string>("image")
                  ?? source.Value<string>("relativeUrl")
                  ?? source.Value<string>("path");

        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var category = source.Value<string>("categorie") ?? source.Value<string>("category");
        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "image", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var contentType = source.Value<string>("contentType")
                         ?? source.Value<string>("mimeType")
                         ?? source.Value<string>("type");

        if (!string.IsNullOrWhiteSpace(contentType)
            && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new AdminImage
        {
            Id = source.Value<int?>("id") ?? 0,
            Url = url,
            Category = category,
            ContentType = contentType,
            DisplayName = source.Value<string>("imageName")
                          ?? source.Value<string>("name")
                          ?? source.Value<string>("title")
                          ?? Path.GetFileName(url)
        };
    }
}
