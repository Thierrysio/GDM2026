using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class ImageUploadViewModel : BaseViewModel
{
    private readonly ImageUploadService _uploadService = new();
    private readonly Apis _apis = new();

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
                fileResult = await MediaPicker.Default.PickPhotoAsync();
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
        catch (Exception)
        {
            StatusMessage = "Impossible de sélectionner la photo. Réessayez.";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsBusy = false;
            (UploadCommand as Command)?.ChangeCanExecute();
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

            var uploadResult = await _uploadService.UploadAsync(_selectedFilePath, "images");

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
}
