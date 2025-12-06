using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using System.Linq;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class ImageUploadViewModel : BaseViewModel
{
    private readonly ImageUploadService _uploadService = new();
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionLoaded;

    private string _statusMessage = "Choisissez une image à envoyer.";
    private Color _statusColor = Colors.Gold;
    private ImageSource? _selectedImage;
    private string? _selectedFilePath;
    private string _fileName = string.Empty;

    public ImageUploadViewModel()
    {
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

            var newFileName = $"{Path.GetFileNameWithoutExtension(fileResult.FileName)}-{Guid.NewGuid():N}{Path.GetExtension(fileResult.FileName)}";
            var newFilePath = Path.Combine(FileSystem.CacheDirectory, newFileName);

            await using (var sourceStream = await fileResult.OpenReadAsync())
            await using (var destStream = File.OpenWrite(newFilePath))
            {
                await sourceStream.CopyToAsync(destStream);
            }

            _selectedFilePath = newFilePath;
            SelectedImage = ImageSource.FromFile(newFilePath);
            FileName = newFileName;
            StatusMessage = "Image prête à être envoyée.";
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
        catch (HttpRequestException)
        {
            StatusMessage = "Impossible de contacter le serveur dantecmarket.com.";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception)
        {
            StatusMessage = "Une erreur est survenue lors de l'envoi.";
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

        if (string.IsNullOrWhiteSpace(_sessionService.AuthToken))
        {
            StatusMessage = "Vous devez vous reconnecter pour envoyer des images.";
            StatusColor = Colors.OrangeRed;
            return false;
        }

        _apis.SetBearerToken(_sessionService.AuthToken);
        _uploadService.SetBearerToken(_sessionService.AuthToken);
        return true;
    }
}
