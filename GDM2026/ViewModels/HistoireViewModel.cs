using GDM2026.Models;
using GDM2026.Services;
using GDM2026.Views;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class HistoireViewModel : BaseViewModel
{
    private enum HistoireFormMode
    {
        None,
        Create,
        Update
    }

    private sealed class HistoireListResponse
    {
        [JsonProperty("data")]
        public List<Histoire>? Data { get; set; }
    }

    private sealed class HistoireResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("data")]
        public Histoire? Data { get; set; }
    }

    private sealed class ApiMessageResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }
    }

    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();
    private readonly ImageUploadService _uploadService = new();

    private bool _sessionReady;
    private HistoireFormMode _currentMode;
    private bool _isLoadingHistoires;
    private string _statusMessage = "Choisissez une action pour commencer.";
    private string _formHeader = "Formulaire";
    private string _formHelperMessage = string.Empty;
    private string _histoiresStatusMessage = "Les histoires ne sont pas encore chargées.";
    private string _imageStatusMessage = "Aucune image sélectionnée.";

    private Histoire? _selectedHistoire;
    private string _titre = string.Empty;
    private string _texte = string.Empty;
    private string _urlImage = string.Empty;
    private string _dateHistoireText = string.Empty;

    private ImageSource? _selectedImagePreview;
    private string? _selectedLocalFile;
    private string _selectedImageLabel = "Aucune image locale.";
    private bool _isImageUploading;

    public HistoireViewModel()
    {
        Histoires = new ObservableCollection<Histoire>();

        ShowCreatePanelCommand = new Command(() => SetMode(HistoireFormMode.Create));
        ShowUpdatePanelCommand = new Command(() => SetMode(HistoireFormMode.Update));
        RefreshHistoiresCommand = new Command(async () => await LoadHistoiresAsync());
        CreateHistoireCommand = new Command(async () => await CreateHistoireAsync(), CanCreateHistoire);
        UpdateHistoireCommand = new Command(async () => await UpdateHistoireAsync(), CanUpdateHistoire);
        DeleteHistoireCommand = new Command(async () => await DeleteHistoireAsync(), CanDeleteHistoire);
        GoBackCommand = new Command(async () => await NavigateBackAsync());

        CapturePhotoCommand = new Command(async () => await PickPhotoAsync(fromCamera: true));
        PickFromLibraryCommand = new Command(async () => await PickPhotoAsync(fromCamera: false));
        UploadImageCommand = new Command(async () => await UploadSelectedImageAsync(), () => HasLocalImageSelection && !_isImageUploading);
    }

    public ObservableCollection<Histoire> Histoires { get; }

    public ICommand ShowCreatePanelCommand { get; }
    public ICommand ShowUpdatePanelCommand { get; }
    public ICommand RefreshHistoiresCommand { get; }
    public ICommand CreateHistoireCommand { get; }
    public ICommand UpdateHistoireCommand { get; }
    public ICommand DeleteHistoireCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand CapturePhotoCommand { get; }
    public ICommand PickFromLibraryCommand { get; }
    public ICommand UploadImageCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string FormHeader
    {
        get => _formHeader;
        set => SetProperty(ref _formHeader, value);
    }

    public string FormHelperMessage
    {
        get => _formHelperMessage;
        set => SetProperty(ref _formHelperMessage, value);
    }

    public string HistoiresStatusMessage
    {
        get => _histoiresStatusMessage;
        set => SetProperty(ref _histoiresStatusMessage, value);
    }

    public bool IsCreateMode => _currentMode == HistoireFormMode.Create;
    public bool IsUpdateMode => _currentMode == HistoireFormMode.Update;
    public bool IsFormSectionVisible => _currentMode is HistoireFormMode.Create or HistoireFormMode.Update;
    public bool IsFormInputEnabled => _currentMode == HistoireFormMode.Create || SelectedHistoire is not null;

    public string FormActionButtonText => _currentMode == HistoireFormMode.Update
        ? "Mettre à jour l'histoire"
        : "Ajouter l'histoire";

    public ICommand FormActionCommand => _currentMode == HistoireFormMode.Update
        ? UpdateHistoireCommand
        : CreateHistoireCommand;

    public bool IsFormActionEnabled => (_currentMode == HistoireFormMode.Update && CanUpdateHistoire())
        || (_currentMode == HistoireFormMode.Create && CanCreateHistoire());

    public bool IsLoadingHistoires
    {
        get => _isLoadingHistoires;
        set
        {
            if (SetProperty(ref _isLoadingHistoires, value))
            {
                RefreshCommands();
            }
        }
    }

    public Histoire? SelectedHistoire
    {
        get => _selectedHistoire;
        set
        {
            if (SetProperty(ref _selectedHistoire, value))
            {
                ApplySelection(value);
                OnPropertyChanged(nameof(HasHistoireSelection));
            }
        }
    }

    public string Titre
    {
        get => _titre;
        set
        {
            if (SetProperty(ref _titre, value))
            {
                RefreshCommands();
            }
        }
    }

    public string Texte
    {
        get => _texte;
        set
        {
            if (SetProperty(ref _texte, value))
            {
                RefreshCommands();
            }
        }
    }

    public string UrlImage
    {
        get => _urlImage;
        set
        {
            if (SetProperty(ref _urlImage, value))
            {
                RefreshCommands();
            }
        }
    }

    public string DateHistoireText
    {
        get => _dateHistoireText;
        set
        {
            if (SetProperty(ref _dateHistoireText, value))
            {
                RefreshCommands();
            }
        }
    }

    public ImageSource? SelectedImagePreview
    {
        get => _selectedImagePreview;
        set => SetProperty(ref _selectedImagePreview, value);
    }

    public string SelectedImageLabel
    {
        get => _selectedImageLabel;
        set => SetProperty(ref _selectedImageLabel, value);
    }

    public string ImageStatusMessage
    {
        get => _imageStatusMessage;
        set => SetProperty(ref _imageStatusMessage, value);
    }

    public bool IsImageUploading
    {
        get => _isImageUploading;
        set
        {
            if (SetProperty(ref _isImageUploading, value))
            {
                (UploadImageCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public bool HasLocalImageSelection => !string.IsNullOrWhiteSpace(_selectedLocalFile);

    public bool HasHistoireSelection => SelectedHistoire is not null;

    public async Task OnPageAppearingAsync()
    {
        await EnsureSessionAsync();
        SetMode(HistoireFormMode.None);
        StatusMessage = "Choisissez une action pour commencer.";
    }

    private async Task EnsureSessionAsync()
    {
        if (_sessionReady)
        {
            return;
        }

        try
        {
            var hasSession = await _sessionService.LoadAsync();
            _apis.SetBearerToken(hasSession ? _sessionService.AuthToken : string.Empty);
            _uploadService.SetBearerToken(hasSession ? _sessionService.AuthToken : string.Empty);
            _sessionReady = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HISTOIRE] Erreur lors du chargement de session : {ex}");
        }
    }

    private void SetMode(HistoireFormMode mode)
    {
        _currentMode = mode;
        OnPropertyChanged(nameof(IsCreateMode));
        OnPropertyChanged(nameof(IsUpdateMode));
        OnPropertyChanged(nameof(IsFormSectionVisible));
        OnPropertyChanged(nameof(IsFormInputEnabled));
        OnPropertyChanged(nameof(FormActionButtonText));
        OnPropertyChanged(nameof(FormActionCommand));
        OnPropertyChanged(nameof(IsFormActionEnabled));

        if (mode == HistoireFormMode.Create)
        {
            FormHeader = "Nouvelle histoire";
            FormHelperMessage = "Renseignez les informations de l'histoire.";
            StatusMessage = "Complétez le formulaire puis validez.";
            ResetForm();
        }
        else if (mode == HistoireFormMode.Update)
        {
            FormHeader = "Mettre à jour une histoire";
            FormHelperMessage = "Choisissez une histoire dans la liste pour préremplir le formulaire.";
            StatusMessage = "Sélectionnez une histoire puis modifiez ses informations.";
            ResetForm();
            _ = LoadHistoiresAsync();
        }
        else
        {
            FormHeader = "Formulaire";
            FormHelperMessage = string.Empty;
            StatusMessage = "Choisissez une action pour commencer.";
            ResetForm();
        }

        RefreshCommands();
    }

    private void ResetForm()
    {
        Titre = string.Empty;
        Texte = string.Empty;
        UrlImage = string.Empty;
        DateHistoireText = string.Empty;
        SelectedHistoire = null;
        ClearLocalImageSelection();
    }

    private void ApplySelection(Histoire? selected)
    {
        if (selected is null)
        {
            FormHelperMessage = "Choisissez une histoire dans la liste pour la modifier.";
            RefreshCommands();
            return;
        }

        Titre = selected.Titre ?? string.Empty;
        Texte = selected.Texte ?? string.Empty;
        UrlImage = selected.UrlImage ?? string.Empty;
        DateHistoireText = selected.DateHistoire?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
        FormHelperMessage = $"Modification de l'histoire #{selected.Id}.";
        RefreshCommands();
    }

    private async Task LoadHistoiresAsync()
    {
        await EnsureSessionAsync();

        IsLoadingHistoires = true;
        HistoiresStatusMessage = "Chargement des histoires…";

        try
        {
            var response = await _apis.PostAsync<object, HistoireListResponse>("/histoire/list", new { });
            var histoires = response?.Data ?? new List<Histoire>();

            Histoires.Clear();
            foreach (var histoire in histoires.OrderByDescending(h => h.DateHistoire ?? DateTime.MinValue))
            {
                Histoires.Add(histoire);
            }

            HistoiresStatusMessage = Histoires.Count == 0
                ? "Aucune histoire chargée pour le moment."
                : $"{Histoires.Count} histoire(s) chargée(s).";
        }
        catch (HttpRequestException httpEx)
        {
            HistoiresStatusMessage = "Impossible de charger les histoires.";
            Debug.WriteLine($"[HISTOIRE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            HistoiresStatusMessage = "Erreur inattendue pendant le chargement.";
            Debug.WriteLine($"[HISTOIRE] Unexpected error: {ex}");
        }
        finally
        {
            IsLoadingHistoires = false;
        }
    }

    private async Task CreateHistoireAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!TryParseDate(out var parsedDate))
        {
            StatusMessage = "Renseignez un titre, un texte, une image et une date valides.";
            return;
        }

        try
        {
            IsBusy = true;
            await EnsureSessionAsync();

            var payload = new
            {
                titre = Titre?.Trim(),
                texte = Texte?.Trim(),
                urlImage = UrlImage?.Trim(),
                dateHistoire = parsedDate.ToString("O", CultureInfo.InvariantCulture),
            };

            var response = await _apis.PostAsync<object, HistoireResponse>("/histoire/create", payload);
            var created = response?.Data ?? new Histoire
            {
                Id = 0,
                Titre = Titre,
                Texte = Texte,
                UrlImage = UrlImage,
                DateHistoire = parsedDate,
            };

            Histoires.Insert(0, created);
            HistoiresStatusMessage = Histoires.Count == 0
                ? "Aucune histoire chargée pour le moment."
                : $"{Histoires.Count} histoire(s) chargée(s).";
            StatusMessage = response?.Message ?? "Histoire créée avec succès.";
            ResetForm();
        }
        catch (HttpRequestException httpEx)
        {
            StatusMessage = "Impossible de créer l'histoire.";
            Debug.WriteLine($"[HISTOIRE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur inattendue lors de la création.";
            Debug.WriteLine($"[HISTOIRE] Unexpected error: {ex}");
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task PickPhotoAsync(bool fromCamera)
    {
        if (IsBusy || IsImageUploading)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ImageStatusMessage = fromCamera
                ? "Ouverture de l'appareil photo…"
                : "Ouverture de la bibliothèque…";

            if (!await EnsurePermissionsAsync(fromCamera))
            {
                ImageStatusMessage = "Autorisez l'accès aux photos pour continuer.";
                return;
            }

            FileResult? fileResult;
            if (fromCamera)
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    ImageStatusMessage = "La capture photo n'est pas supportée sur cet appareil.";
                    return;
                }

                fileResult = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = $"histoire-{DateTimeOffset.Now:yyyyMMddHHmmss}"
                });
            }
            else
            {
                var photos = await MediaPicker.Default.PickPhotosAsync();
                fileResult = photos?.FirstOrDefault();
            }

            if (fileResult is null)
            {
                ImageStatusMessage = "Sélection annulée.";
                return;
            }

            ImageStatusMessage = "Optimisation de l'image…";

            _selectedLocalFile = await SaveFileToLocalAsync(fileResult);
            SelectedImagePreview = ImageSource.FromFile(_selectedLocalFile);
            SelectedImageLabel = Path.GetFileName(_selectedLocalFile);
            ImageStatusMessage = "Image prête à être envoyée.";
            OnPropertyChanged(nameof(HasLocalImageSelection));
        }
        catch (FeatureNotSupportedException)
        {
            ImageStatusMessage = "Fonctionnalité photo non supportée sur cet appareil.";
        }
        catch (PermissionException)
        {
            ImageStatusMessage = "Autorisez l'accès à l'appareil photo ou aux images.";
        }
        catch (TaskCanceledException)
        {
            ImageStatusMessage = "Sélection annulée.";
        }
        catch (Exception ex)
        {
            ImageStatusMessage = "Impossible de sélectionner la photo.";
            Debug.WriteLine($"[HISTOIRE] Sélection image échouée : {ex}");
        }
        finally
        {
            IsBusy = false;
            (UploadImageCommand as Command)?.ChangeCanExecute();
        }
    }

    private async Task UploadSelectedImageAsync()
    {
        if (!HasLocalImageSelection || IsImageUploading)
        {
            return;
        }

        try
        {
            IsImageUploading = true;
            ImageStatusMessage = "Envoi de l'image…";

            await EnsureSessionAsync();
            if (string.IsNullOrWhiteSpace(_selectedLocalFile))
            {
                ImageStatusMessage = "Aucune image locale à envoyer.";
                return;
            }

            var result = await _uploadService.UploadAsync(_selectedLocalFile, "images");
            var relativePath = string.IsNullOrWhiteSpace(result.RelativeUrl)
                ? $"images/{result.FileName}"
                : result.RelativeUrl.TrimStart('/')
                    .Replace("\\", "/");

            UrlImage = relativePath.StartsWith("images/", StringComparison.OrdinalIgnoreCase)
                ? relativePath
                : $"images/{Path.GetFileName(relativePath)}";
            ImageStatusMessage = "Image envoyée. L'URL a été préremplie.";
            StatusMessage = "Image envoyée et associée à l'histoire.";
        }
        catch (HttpRequestException httpEx)
        {
            ImageStatusMessage = "Impossible d'envoyer l'image.";
            Debug.WriteLine($"[HISTOIRE] Upload HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            ImageStatusMessage = "Erreur inattendue pendant l'envoi.";
            Debug.WriteLine($"[HISTOIRE] Upload error: {ex}");
        }
        finally
        {
            IsImageUploading = false;
            (UploadImageCommand as Command)?.ChangeCanExecute();
            OnPropertyChanged(nameof(HasLocalImageSelection));
            RefreshCommands();
        }
    }

    private static async Task<string> SaveFileToLocalAsync(FileResult fileResult)
    {
        if (fileResult is null)
        {
            throw new ArgumentNullException(nameof(fileResult));
        }

        return await OptimizeAndSaveAsync(fileResult);
    }

    private static async Task<string> OptimizeAndSaveAsync(FileResult fileResult)
    {
        await using var sourceStream = await fileResult.OpenReadAsync();

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

        var resized = originalBitmap.Resize(new SKImageInfo(newWidth, newHeight, originalBitmap.ColorType, originalBitmap.AlphaType), SKFilterQuality.High);
        return resized ?? originalBitmap;
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

                var writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                if (writeStatus != PermissionStatus.Granted)
                {
                    writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                }

                var readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                if (readStatus != PermissionStatus.Granted)
                {
                    readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
                }

                return cameraStatus == PermissionStatus.Granted
                    && writeStatus == PermissionStatus.Granted
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

            var storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            if (storageStatus != PermissionStatus.Granted)
            {
                storageStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
            }

            return storageStatus == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HISTOIRE] Permissions error: {ex}");
            return false;
        }
    }

    private void ClearLocalImageSelection()
    {
        _selectedLocalFile = null;
        SelectedImagePreview = null;
        SelectedImageLabel = "Aucune image locale.";
        ImageStatusMessage = "Aucune image sélectionnée.";
        (UploadImageCommand as Command)?.ChangeCanExecute();
        OnPropertyChanged(nameof(HasLocalImageSelection));
    }

    private async Task UpdateHistoireAsync()
    {
        if (IsBusy || SelectedHistoire is null)
        {
            return;
        }

        if (!TryParseDate(out var parsedDate))
        {
            StatusMessage = "Renseignez un titre, un texte, une image et une date valides.";
            return;
        }

        try
        {
            IsBusy = true;
            await EnsureSessionAsync();

            var payload = new
            {
                id = SelectedHistoire.Id,
                titre = Titre?.Trim(),
                texte = Texte?.Trim(),
                urlImage = UrlImage?.Trim(),
                dateHistoire = parsedDate.ToString("O", CultureInfo.InvariantCulture),
            };

            var response = await _apis.PostAsync<object, HistoireResponse>("/histoire/update", payload);
            var updated = response?.Data;

            var targetId = SelectedHistoire.Id;
            var existing = Histoires.FirstOrDefault(h => h.Id == targetId);
            if (existing is not null)
            {
                existing.Titre = updated?.Titre ?? Titre;
                existing.Texte = updated?.Texte ?? Texte;
                existing.UrlImage = updated?.UrlImage ?? UrlImage;
                existing.DateHistoire = updated?.DateHistoire ?? parsedDate;
                SelectedHistoire = existing;
            }

            StatusMessage = response?.Message ?? "Histoire mise à jour.";
        }
        catch (HttpRequestException httpEx)
        {
            StatusMessage = "Impossible de mettre à jour cette histoire.";
            Debug.WriteLine($"[HISTOIRE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur inattendue lors de la mise à jour.";
            Debug.WriteLine($"[HISTOIRE] Unexpected error: {ex}");
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task DeleteHistoireAsync()
    {
        if (IsBusy || SelectedHistoire is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await EnsureSessionAsync();

            var payload = new { id = SelectedHistoire.Id };
            var response = await _apis.PostAsync<object, ApiMessageResponse>("/histoire/delete", payload);

            var toRemove = SelectedHistoire;
            SelectedHistoire = null;
            Histoires.Remove(toRemove);
            HistoiresStatusMessage = Histoires.Count == 0
                ? "Aucune histoire chargée pour le moment."
                : $"{Histoires.Count} histoire(s) chargée(s).";
            StatusMessage = response?.Message ?? "Histoire supprimée.";
        }
        catch (HttpRequestException httpEx)
        {
            StatusMessage = "Impossible de supprimer cette histoire.";
            Debug.WriteLine($"[HISTOIRE] HTTP error: {httpEx}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Erreur inattendue lors de la suppression.";
            Debug.WriteLine($"[HISTOIRE] Unexpected error: {ex}");
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task NavigateBackAsync()
    {
        if (Shell.Current?.Navigation?.NavigationStack?.Count > 1)
        {
            await Shell.Current.GoToAsync("..", animate: true);
        }
        else if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync(nameof(HomePage), animate: true);
        }
    }

    private bool TryParseDate(out DateTime parsedDate)
    {
        parsedDate = default;

        if (string.IsNullOrWhiteSpace(Titre)
            || string.IsNullOrWhiteSpace(Texte)
            || string.IsNullOrWhiteSpace(UrlImage))
        {
            return false;
        }

        return DateTime.TryParse(DateHistoireText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsedDate);
    }

    private bool CanCreateHistoire()
    {
        return !IsBusy && !IsLoadingHistoires && TryParseDate(out _);
    }

    private bool CanUpdateHistoire()
    {
        return !IsBusy && !IsLoadingHistoires && SelectedHistoire is not null && TryParseDate(out _);
    }

    private bool CanDeleteHistoire()
    {
        return !IsBusy && !IsLoadingHistoires && SelectedHistoire is not null;
    }

    private void RefreshCommands()
    {
        (CreateHistoireCommand as Command)?.ChangeCanExecute();
        (UpdateHistoireCommand as Command)?.ChangeCanExecute();
        (DeleteHistoireCommand as Command)?.ChangeCanExecute();
        (UploadImageCommand as Command)?.ChangeCanExecute();
        OnPropertyChanged(nameof(IsFormActionEnabled));
    }
}
