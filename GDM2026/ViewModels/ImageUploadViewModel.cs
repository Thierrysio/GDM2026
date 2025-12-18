using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using SkiaSharp;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class ImageUploadViewModel : BaseViewModel
{
    private readonly ImageUploadService _uploadService;
    private readonly Apis _apis;
    private readonly SessionService _sessionService = new();

    private bool _sessionLoaded;

    private string _statusMessage = "Choisissez une image à envoyer.";
    private Color _statusColor = Colors.Gold;
    private ImageSource? _selectedImage;
    private string? _selectedFilePath;
    private string _fileName = string.Empty;

    public ImageUploadViewModel()
    {
        _apis = new();
        _uploadService = new ImageUploadService(_apis.HttpClient);

        CapturePhotoCommand = new Command(async () => await PickPhotoAsync(fromCamera: true));
        PickFromGalleryCommand = new Command(async () => await PickPhotoAsync(fromCamera: false));
        UploadCommand = new Command(async () => await UploadAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(_selectedFilePath));
    }

    public ICommand CapturePhotoCommand { get; }

    public ICommand PickFromGalleryCommand { get; }

    public ICommand UploadCommand { get; }

    public ImageSource? SelectedImage
    {
        get => _selectedImage;
        set => SetProperty(ref _selectedImage, value);
    }

    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
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

    private async Task PickPhotoAsync(bool fromCamera)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
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
                    Title = $"image-{DateTimeOffset.Now:yyyyMMddHHmmss}"
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

            // Exécuter l'optimisation lourde hors du thread UI
            var optimizedFilePath = await OptimizeAndSaveAsync(fileResult);

            _selectedFilePath = optimizedFilePath;
            SelectedImage = ImageSource.FromFile(optimizedFilePath);
            FileName = Path.GetFileName(optimizedFilePath);
            StatusMessage = "Image compressée et prête à être envoyée.";
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
            (UploadCommand as Command)?.ChangeCanExecute();
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

    private async Task UploadAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(_selectedFilePath))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Envoi de l'image en cours…";
            StatusColor = Colors.Gold;
            (UploadCommand as Command)?.ChangeCanExecute();

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
            var uploadResult = await _uploadService.UploadAsync(uploadStream, FileName, "images");

            var payload = new
            {
                url = uploadResult.RelativeUrl,
                imageName = uploadResult.FileName
            };

            var success = await _apis.PostBoolAsync("/api/crud/images/create", payload);

            StatusMessage = success
                ? "Image envoyée et enregistrée avec succès."
                : "Envoi terminé mais la sauvegarde a échoué.";
            StatusColor = success ? Colors.LightGreen : Colors.OrangeRed;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Envoi annulé.";
            StatusColor = Colors.OrangeRed;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Debug.WriteLine($"[UPLOAD] Jeton refusé : {ex}");
            StatusMessage = "Authentification refusée (401). Le jeton enregistré est conservé. Reconnectez-vous depuis cette page puis relancez l'envoi.";
            StatusColor = Colors.OrangeRed;

            if (await PromptInlineLoginAsync())
            {
                ApplyAuthToken();
                StatusMessage = "Connexion rétablie. Relancez l'envoi de l'image.";
                StatusColor = Colors.LightGreen;
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[UPLOAD] HTTP error: {ex}");
            StatusMessage = $"Impossible de contacter le serveur dantecmarket.com. ({ex.Message})";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Upload failed: {ex}");
            StatusMessage = $"Une erreur est survenue lors de l'envoi : {ex.Message}";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsBusy = false;
            (UploadCommand as Command)?.ChangeCanExecute();
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

        StatusMessage = "Vous devez vous reconnecter pour envoyer des images.";
        StatusColor = Colors.OrangeRed;

        if (!await PromptInlineLoginAsync())
        {
            StatusMessage = "Connexion requise pour envoyer des images.";
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
            Debug.WriteLine($"[LOGIN] user.Token = '{user?.Token}'");
            if (user is null)
            {
                StatusMessage = "Identifiants invalides. Merci de réessayer depuis cette page.";
                StatusColor = Colors.OrangeRed;
                return false;
            }

            await _sessionService.SaveAsync(user, user.Token);

            if (string.IsNullOrWhiteSpace(_sessionService.AuthToken))
            {
                StatusMessage = "Connexion effectuée, mais aucun jeton d'authentification n'a été reçu du serveur. Impossible d'envoyer l'image.";
                StatusColor = Colors.OrangeRed;
                return false;
            }

            StatusMessage = "Connexion réussie. Reprise de l'envoi.";
            StatusColor = Colors.LightGreen;
            return true;
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "Délai d'authentification dépassé. Vérifiez votre connexion.";
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
            "Identifiez-vous pour envoyer des images.",
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
            // Utilisation d'un chemin relatif pour éviter les doubles slash et
            // profiter de la BaseAddress configurée dans Apis.
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

        // Déplacer le décodage/compression SkiaSharp hors du thread UI pour éviter les blocages
        var resultPath = await Task.Run(() =>
        {
            using var managedStream = new SKManagedStream(sourceStream);
            using var originalBitmap = SKBitmap.Decode(managedStream) ?? throw new InvalidOperationException("Impossible de lire l'image sélectionnée.");

            var resizedBitmap = ResizeBitmap(originalBitmap, 1280);

            var newFileName = $"{Path.GetFileNameWithoutExtension(fileResult.FileName)}-{Guid.NewGuid():N}.jpg";
            var newFilePath = Path.Combine(FileSystem.CacheDirectory, newFileName);

            using var image = SKImage.FromBitmap(resizedBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
            using (var destStream = File.Open(newFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                data.SaveTo(destStream);
            }

            if (!ReferenceEquals(resizedBitmap, originalBitmap))
            {
                resizedBitmap.Dispose();
            }

            return newFilePath;
        }).ConfigureAwait(false);

        return resultPath;
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

        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        var resized = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight, originalBitmap.ColorType, originalBitmap.AlphaType), sampling);
        return resized ?? originalBitmap;
    }
}
