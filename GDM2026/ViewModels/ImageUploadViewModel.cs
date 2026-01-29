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
    private string _editableFileName = string.Empty;
    private bool _showOriginalFileName;

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

    public string EditableFileName
    {
        get => _editableFileName;
        set => SetProperty(ref _editableFileName, value);
    }

    public bool ShowOriginalFileName
    {
        get => _showOriginalFileName;
        set => SetProperty(ref _showOriginalFileName, value);
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

            FileResult? fileResult;
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

            // CRITIQUE: Copier le fichier IMMÉDIATEMENT après la capture.
            // Sur Android 13+, le stream du FileResult peut être fermé très rapidement.
            // On doit le lire tout de suite, avant de faire quoi que ce soit d'autre.
            var originalFileName = fileResult.FileName;
            var tempFileName = $"capture-{Guid.NewGuid():N}.tmp";
            var tempFilePath = Path.Combine(FileSystem.CacheDirectory, tempFileName);

            Debug.WriteLine($"[PHOTO] Copie immédiate du fichier: {originalFileName}");

            await CopyFileResultToTempAsync(fileResult, tempFilePath);

            Debug.WriteLine($"[PHOTO] Fichier copié vers: {tempFilePath}, taille: {new FileInfo(tempFilePath).Length} octets");

            // Maintenant on peut optimiser depuis le fichier local (plus de risque de stream fermé)
            var optimizedFilePath = await OptimizeFromLocalFileAsync(tempFilePath, originalFileName);

            // Nettoyer le fichier temporaire
            try { File.Delete(tempFilePath); } catch { /* ignore */ }

            _selectedFilePath = optimizedFilePath;
            SelectedImage = ImageSource.FromFile(optimizedFilePath);
            FileName = Path.GetFileName(optimizedFilePath);

            // Extraire le nom sans extension et sans le GUID pour l'édition
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(FileName);
            // Retirer le GUID (format: nom-guid)
            var lastDashIndex = nameWithoutExtension.LastIndexOf('-');
            var cleanName = lastDashIndex > 0 ? nameWithoutExtension.Substring(0, lastDashIndex) : nameWithoutExtension;
            EditableFileName = cleanName;
            ShowOriginalFileName = true;

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
        catch (ObjectDisposedException)
        {
            StatusMessage = "Impossible de lire la photo sélectionnée. Réessayez.";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PHOTO] Erreur lors de la sélection: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"[PHOTO] StackTrace: {ex.StackTrace}");

            // Message plus précis selon le type d'erreur
            if (ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("permission", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Accès refusé. Autorisez l'application dans les paramètres de votre appareil.";
            }
            else if (ex is IOException || ex is UnauthorizedAccessException)
            {
                StatusMessage = "Impossible d'accéder au fichier. Vérifiez les autorisations de stockage.";
            }
            else
            {
                StatusMessage = $"Erreur lors de la sélection de la photo: {ex.Message}";
            }
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsBusy = false;
            (UploadCommand as Command)?.ChangeCanExecute();
        }
    }

    private static async Task CopyFileResultToTempAsync(FileResult fileResult, string tempFilePath)
    {
        var attempts = 0;
        while (attempts < 2)
        {
            attempts++;
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }

                await using (var sourceStream = await fileResult.OpenReadAsync())
                await using (var destStream = File.Create(tempFilePath))
                {
                    await sourceStream.CopyToAsync(destStream);
                }

                return;
            }
            catch (ObjectDisposedException)
            {
                Debug.WriteLine($"[PHOTO] Stream fermé lors de la tentative {attempts}.");
            }
        }

        if (!string.IsNullOrWhiteSpace(fileResult.FullPath) && File.Exists(fileResult.FullPath))
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            await using var sourceStream = File.OpenRead(fileResult.FullPath);
            await using var destStream = File.Create(tempFilePath);
            await sourceStream.CopyToAsync(destStream);
            return;
        }

        throw new IOException("Impossible de lire la photo sélectionnée. Réessayez.");
    }

    private static async Task<bool> EnsurePermissionsAsync(bool fromCamera)
    {
        try
        {
            if (fromCamera)
            {
                // Permission Camera toujours nécessaire
                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (cameraStatus != PermissionStatus.Granted)
                {
                    return false;
                }

                // Sur Android < 13 (API < 33), les permissions storage sont nécessaires
                // pour accéder au fichier photo créé par l'intent caméra.
                // Sur Android 13+, ces permissions sont obsolètes et non requises.
#if ANDROID
                if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.Tiramisu)
                {
                    var storageWriteStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                    if (storageWriteStatus != PermissionStatus.Granted)
                    {
                        storageWriteStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                    }

                    var storageReadStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                    if (storageReadStatus != PermissionStatus.Granted)
                    {
                        storageReadStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
                    }

                    // Sur les anciennes versions, on a besoin des deux permissions
                    return storageWriteStatus == PermissionStatus.Granted
                        && storageReadStatus == PermissionStatus.Granted;
                }
#endif
                return true; // Android 13+ ou autres plateformes
            }

            // Pour la sélection depuis la galerie
            var photosStatus = await Permissions.CheckStatusAsync<Permissions.Photos>();
            if (photosStatus != PermissionStatus.Granted)
            {
                photosStatus = await Permissions.RequestAsync<Permissions.Photos>();
            }

            if (photosStatus == PermissionStatus.Granted || photosStatus == PermissionStatus.Limited)
            {
                return true;
            }

            // Fallback pour les anciennes versions Android
            var readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            if (readStatus != PermissionStatus.Granted)
            {
                readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
            }

#if ANDROID
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
            {
                return true; // Le Photo Picker fonctionne sans permission sur Android 13+
            }
#endif
            return readStatus == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PHOTO] Erreur permissions: {ex.Message}");
            return true; // Laisser MediaPicker tenter l'opération
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

            // Construire le nouveau nom de fichier avec le nom éditable
            var extension = Path.GetExtension(_selectedFilePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg"; // Extension par défaut
            }

            var finalFileName = FileName; // Utiliser le nom original par défaut

            if (!string.IsNullOrWhiteSpace(EditableFileName))
            {
                // Nettoyer le nom éditable des caractères invalides
                var cleanedName = EditableFileName.Trim();
                var invalidChars = Path.GetInvalidFileNameChars();
                foreach (var c in invalidChars)
                {
                    cleanedName = cleanedName.Replace(c, '_');
                }

                if (!string.IsNullOrWhiteSpace(cleanedName))
                {
                    finalFileName = $"{cleanedName}{extension}";
                }
            }

            Debug.WriteLine($"[UPLOAD] Nom de fichier final : {finalFileName}");
            Debug.WriteLine($"[UPLOAD] Chemin du fichier : {_selectedFilePath}");

            await using var uploadStream = File.OpenRead(_selectedFilePath);
            var uploadResult = await _uploadService.UploadAsync(uploadStream, finalFileName, "images");

            // Extraire uniquement le nom du fichier sans le préfixe /images/
            var imageFileName = uploadResult.FileName;

            var payload = new
            {
                url = imageFileName,  // Envoyer uniquement le nom du fichier
                imageName = imageFileName
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

    /// <summary>
    /// Optimise une image depuis un fichier local (déjà copié dans le cache).
    /// Cette méthode travaille uniquement avec des fichiers locaux, évitant les problèmes
    /// de stream fermé sur Android 13+.
    /// </summary>
    private static async Task<string> OptimizeFromLocalFileAsync(string localFilePath, string originalFileName)
    {
        var resultPath = await Task.Run(() =>
        {
            // Lire le fichier directement depuis le disque
            byte[] imageData = File.ReadAllBytes(localFilePath);

            if (imageData.Length == 0)
            {
                throw new InvalidOperationException("Le fichier image est vide ou n'a pas pu être lu.");
            }

            Debug.WriteLine($"[PHOTO] Optimisation de l'image: {imageData.Length} octets");

            using var memoryStream = new MemoryStream(imageData);

            // Lire l'orientation EXIF avec SKCodec
            var orientation = SKEncodedOrigin.TopLeft;
            using (var codec = SKCodec.Create(memoryStream))
            {
                if (codec != null)
                {
                    orientation = codec.EncodedOrigin;
                }
            }

            // Remettre le flux au début pour le décodage
            memoryStream.Position = 0;
            using var managedStream = new SKManagedStream(memoryStream);
            using var originalBitmap = SKBitmap.Decode(managedStream) ?? throw new InvalidOperationException("Impossible de lire l'image sélectionnée.");

            // Appliquer l'orientation EXIF pour corriger la rotation
            var orientedBitmap = ApplyExifOrientation(originalBitmap, orientation);

            var resizedBitmap = ResizeBitmap(orientedBitmap, 1280);

            var newFileName = $"{Path.GetFileNameWithoutExtension(originalFileName)}-{Guid.NewGuid():N}.jpg";
            var newFilePath = Path.Combine(FileSystem.CacheDirectory, newFileName);

            using var image = SKImage.FromBitmap(resizedBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
            using (var destStream = File.Open(newFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                data.SaveTo(destStream);
            }

            if (!ReferenceEquals(resizedBitmap, orientedBitmap))
            {
                resizedBitmap.Dispose();
            }
            if (!ReferenceEquals(orientedBitmap, originalBitmap))
            {
                orientedBitmap.Dispose();
            }

            Debug.WriteLine($"[PHOTO] Image optimisée sauvegardée: {newFilePath}");
            return newFilePath;
        }).ConfigureAwait(false);

        return resultPath;
    }

    private static SKBitmap ApplyExifOrientation(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft || origin == SKEncodedOrigin.Default)
        {
            return bitmap;
        }

        SKBitmap rotated;
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: // Flip horizontal
                rotated = new SKBitmap(bitmap.Width, bitmap.Height, bitmap.ColorType, bitmap.AlphaType);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Scale(-1, 1, bitmap.Width / 2f, 0);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                break;
            case SKEncodedOrigin.BottomRight: // Rotation 180°
                rotated = new SKBitmap(bitmap.Width, bitmap.Height, bitmap.ColorType, bitmap.AlphaType);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.RotateDegrees(180, bitmap.Width / 2f, bitmap.Height / 2f);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                break;
            case SKEncodedOrigin.BottomLeft: // Flip vertical
                rotated = new SKBitmap(bitmap.Width, bitmap.Height, bitmap.ColorType, bitmap.AlphaType);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Scale(1, -1, 0, bitmap.Height / 2f);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                break;
            case SKEncodedOrigin.LeftTop: // Transpose (rotate 90 CW + flip horizontal)
                rotated = new SKBitmap(bitmap.Height, bitmap.Width, bitmap.ColorType, bitmap.AlphaType);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Translate(rotated.Width, 0);
                    canvas.RotateDegrees(90);
                    canvas.Scale(1, -1, 0, bitmap.Height / 2f);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                break;
            case SKEncodedOrigin.RightTop: // Rotation 90° horaire
                rotated = new SKBitmap(bitmap.Height, bitmap.Width, bitmap.ColorType, bitmap.AlphaType);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Translate(rotated.Width, 0);
                    canvas.RotateDegrees(90);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                break;
            case SKEncodedOrigin.RightBottom: // Transverse (rotate 90 CCW + flip horizontal)
                rotated = new SKBitmap(bitmap.Height, bitmap.Width, bitmap.ColorType, bitmap.AlphaType);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Translate(0, rotated.Height);
                    canvas.RotateDegrees(-90);
                    canvas.Scale(1, -1, 0, bitmap.Height / 2f);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                break;
            case SKEncodedOrigin.LeftBottom: // Rotation 90° anti-horaire
                rotated = new SKBitmap(bitmap.Height, bitmap.Width, bitmap.ColorType, bitmap.AlphaType);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Translate(0, rotated.Height);
                    canvas.RotateDegrees(-90);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                break;
            default:
                return bitmap;
        }

        return rotated;
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
