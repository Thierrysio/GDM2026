using GDM2026.Models;
using GDM2026.Services;
using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using SkiaSharp;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class ActualiteViewModel : BaseViewModel
{
    private readonly Apis _apis;
    private readonly ImageUploadService _uploadService;
    private readonly SessionService _sessionService = new();

    private bool _sessionLoaded;
    private bool _isFormVisible;
    private string _actualiteTitle = string.Empty;
    private string _actualiteContent = string.Empty;
    private string _statusMessage = "Ajoutez une actualité avec une image pour la publier.";
    private Color _statusColor = Colors.Gold;
    private ImageSource? _selectedImage;
    private string? _selectedFilePath;
    private string _selectedImageName = "Aucune image sélectionnée.";

    public ActualiteViewModel()
    {
        _apis = new Apis();
        _uploadService = new ImageUploadService(_apis.HttpClient);

        ToggleFormCommand = new Command(() => IsFormVisible = !IsFormVisible);
        CapturePhotoCommand = new Command(async () => await PickPhotoAsync(fromCamera: true), () => !IsBusy);
        PickFromGalleryCommand = new Command(async () => await PickPhotoAsync(fromCamera: false), () => !IsBusy);
        SubmitCommand = new Command(async () => await SubmitAsync(), CanSubmit);
    }

    public ICommand ToggleFormCommand { get; }

    public ICommand CapturePhotoCommand { get; }

    public ICommand PickFromGalleryCommand { get; }

    public ICommand SubmitCommand { get; }

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
            && !string.IsNullOrWhiteSpace(_actualiteTitle)
            && !string.IsNullOrWhiteSpace(_selectedFilePath);
    }

    private void RefreshSubmitAvailability()
    {
        (SubmitCommand as Command)?.ChangeCanExecute();
    }

    private async Task PickPhotoAsync(bool fromCamera)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            RefreshSubmitAvailability();

            StatusMessage = fromCamera ? "Ouverture de l'appareil photo…" : "Ouverture de la bibliothèque…";
            StatusColor = Colors.Gold;

            if (!await EnsurePermissionsAsync(fromCamera))
            {
                StatusMessage = "Autorisez l'accès à l'appareil photo ou à la galerie pour continuer.";
                StatusColor = Colors.OrangeRed;
                return;
            }

            FileResult fileResult;
            if (fromCamera)
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    StatusMessage = "La capture photo n'est pas supportée sur cet appareil.";
                    StatusColor = Colors.OrangeRed;
                    return;
                }

                fileResult = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = $"actualite-{DateTimeOffset.Now:yyyyMMddHHmmss}"
                });
            }
            else
            {
                var selectedPhotos = await MediaPicker.Default.PickPhotosAsync();
                fileResult = selectedPhotos?.FirstOrDefault();
            }

            if (fileResult == null)
            {
                StatusMessage = "Sélection annulée.";
                StatusColor = Colors.Gold;
                return;
            }

            var optimizedFilePath = await OptimizeAndSaveAsync(fileResult);
            _selectedFilePath = optimizedFilePath;
            SelectedImage = ImageSource.FromFile(optimizedFilePath);
            SelectedImageName = Path.GetFileName(optimizedFilePath);
            StatusMessage = "Image prête pour la publication.";
            StatusColor = Colors.Gold;
        }
        catch (FeatureNotSupportedException)
        {
            StatusMessage = "La fonctionnalité photo n'est pas supportée sur cet appareil.";
            StatusColor = Colors.OrangeRed;
        }
        catch (PermissionException)
        {
            StatusMessage = "Autorisez l'accès à l'appareil photo ou à la galerie pour continuer.";
            StatusColor = Colors.OrangeRed;
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "Sélection annulée.";
            StatusColor = Colors.Gold;
        }
        catch (Exception)
        {
            StatusMessage = "Impossible de sélectionner la photo. Vérifiez les autorisations de stockage et réessayez.";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsBusy = false;
            RefreshSubmitAvailability();
        }
    }

    private static async Task<bool> EnsurePermissionsAsync(bool fromCamera)
    {
        try
        {
            if (fromCamera)
            {
                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                }

                var storageWriteStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                if (storageWriteStatus != PermissionStatus.Granted)
                {
                    storageWriteStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                }

                var readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                if (readStatus != PermissionStatus.Granted)
                {
                    readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
                }

                return cameraStatus == PermissionStatus.Granted
                    && storageWriteStatus == PermissionStatus.Granted
                    && readStatus == PermissionStatus.Granted;
            }

            var photosStatus = await Permissions.CheckStatusAsync<Permissions.Photos>();
            if (photosStatus != PermissionStatus.Granted)
            {
                photosStatus = await Permissions.RequestAsync<Permissions.Photos>();
            }

            if (photosStatus == PermissionStatus.Granted)
            {
                return true;
            }

            var storageReadStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            if (storageReadStatus != PermissionStatus.Granted)
            {
                storageReadStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
            }

            return storageReadStatus == PermissionStatus.Granted;
        }
        catch (Exception)
        {
            return false;
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

        if (string.IsNullOrWhiteSpace(_selectedFilePath))
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

            if (!await _uploadService.TestConnectivityAsync())
            {
                StatusMessage = "Impossible de contacter le serveur dantecmarket.com.";
                StatusColor = Colors.OrangeRed;
                return;
            }

            await using var uploadStream = File.OpenRead(_selectedFilePath);
            var uploadResult = await _uploadService.UploadAsync(uploadStream, SelectedImageName, "actualites");

            var imagePayload = new
            {
                url = uploadResult.RelativeUrl,
                imageName = uploadResult.FileName
            };

            var imageSaved = await _apis.PostBoolAsync("/api/crud/images/create", imagePayload);
            if (!imageSaved)
            {
                StatusMessage = "Image envoyée mais non enregistrée côté serveur.";
                StatusColor = Colors.OrangeRed;
                return;
            }

            var creationPayload = new
            {
                titre = _actualiteTitle.Trim(),
                description = _actualiteContent.Trim(),
                image = uploadResult.RelativeUrl
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
                _selectedFilePath = null;
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
        _uploadService.SetBearerToken(_sessionService.AuthToken);
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

    private static async Task<string> OptimizeAndSaveAsync(FileResult fileResult)
    {
        await using var sourceStream = await fileResult.OpenReadAsync();
        using var managedStream = new SKManagedStream(sourceStream);
        using var originalBitmap = SKBitmap.Decode(managedStream) ?? throw new InvalidOperationException("Impossible de lire l'image sélectionnée.");

        var resizedBitmap = ResizeBitmap(originalBitmap, 1280);

        var newFileName = $"{Path.GetFileNameWithoutExtension(fileResult.FileName)}-{Guid.NewGuid():N}.jpg";
        var newFilePath = Path.Combine(FileSystem.CacheDirectory, newFileName);

        using var image = SKImage.FromBitmap(resizedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        await using (var destStream = File.Open(newFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            data.SaveTo(destStream);
        }

        if (!ReferenceEquals(resizedBitmap, originalBitmap))
        {
            resizedBitmap.Dispose();
        }

        return newFilePath;
    }

    private static SKBitmap ResizeBitmap(SKBitmap originalBitmap, int maxSize)
    {
        if (originalBitmap.Width <= maxSize && originalBitmap.Height <= maxSize)
        {
            return originalBitmap;
        }

        var scale = Math.Min(maxSize / (float)originalBitmap.Width, maxSize / (float)originalBitmap.Height);
        var newWidth = Math.Max(1, (int)Math.Round(originalBitmap.Width * scale));
        var newHeight = Math.Max(1, (int)Math.Round(originalBitmap.Height * scale));

        var resized = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight, originalBitmap.ColorType, originalBitmap.AlphaType), SKFilterQuality.High);
        return resized ?? originalBitmap;
    }
}
