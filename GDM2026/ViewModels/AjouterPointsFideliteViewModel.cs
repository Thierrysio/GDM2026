using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Net.Http;
using System.Windows.Input;
using ZXing.Net.Maui;

namespace GDM2026.ViewModels;

public class AjouterPointsFideliteViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _isScanning = true;
    private bool _isLoading;
    private bool _isTorchOn;
    private string _statusMessage = "Scannez le QR code du client";
    private bool _hasProcessedCode;
    private bool _isInitialized;
    private bool _showClientInfo;
    private bool _showAmountForm;

    private LoyaltyInfo? _currentClient;
    private string _clientName = string.Empty;
    private string _clientPoints = string.Empty;
    private string _clientPointsInEuros = string.Empty;
    private string _amountText = string.Empty;
    private decimal _amountInEuros;
    private int _pointsToAdd;
    private bool _showConfirmation;
    private string _confirmationMessage = string.Empty;

    // Rush Mode properties
    private bool _isRushModeActive;
    private bool _showRushConfig;
    private string _rushAmountText = string.Empty;
    private decimal _rushAmount;
    private bool _showRushStatus;
    private bool _rushStatusSuccess;
    private string _rushStatusMessage = string.Empty;

    public AjouterPointsFideliteViewModel()
    {
        CancelCommand = new Command(async () => await CancelAsync());
        ToggleTorchCommand = new Command(ToggleTorch);
        ValidateAmountCommand = new Command(async () => await ValidateAmountAsync());
        AddPointsCommand = new Command(async () => await AddPointsAsync());
        CancelAddCommand = new Command(CancelAdd);
        StartNewTransactionCommand = new Command(StartNewTransaction);

        // Rush Mode commands
        OpenRushConfigCommand = new Command(OpenRushConfig);
        ValidateRushAmountCommand = new Command(ValidateRushAmount);
        CloseRushConfigCommand = new Command(CloseRushConfig);
        DeactivateRushModeCommand = new Command(DeactivateRushMode);
        HideRushStatusCommand = new Command(HideRushStatus);

        ScannerOptions = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false
        };
    }

    public BarcodeReaderOptions ScannerOptions { get; }

    public ICommand CancelCommand { get; }
    public ICommand ToggleTorchCommand { get; }
    public ICommand ValidateAmountCommand { get; }
    public ICommand AddPointsCommand { get; }
    public ICommand CancelAddCommand { get; }
    public ICommand StartNewTransactionCommand { get; }

    // Rush Mode commands
    public ICommand OpenRushConfigCommand { get; }
    public ICommand ValidateRushAmountCommand { get; }
    public ICommand CloseRushConfigCommand { get; }
    public ICommand DeactivateRushModeCommand { get; }
    public ICommand HideRushStatusCommand { get; }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool ShowClientInfo
    {
        get => _showClientInfo;
        set => SetProperty(ref _showClientInfo, value);
    }

    public bool ShowAmountForm
    {
        get => _showAmountForm;
        set => SetProperty(ref _showAmountForm, value);
    }

    public bool ShowConfirmation
    {
        get => _showConfirmation;
        set => SetProperty(ref _showConfirmation, value);
    }

    public string ClientName
    {
        get => _clientName;
        set => SetProperty(ref _clientName, value);
    }

    public string ClientPoints
    {
        get => _clientPoints;
        set => SetProperty(ref _clientPoints, value);
    }

    public string ClientPointsInEuros
    {
        get => _clientPointsInEuros;
        set => SetProperty(ref _clientPointsInEuros, value);
    }

    public string AmountText
    {
        get => _amountText;
        set => SetProperty(ref _amountText, value);
    }

    public string ConfirmationMessage
    {
        get => _confirmationMessage;
        set => SetProperty(ref _confirmationMessage, value);
    }

    // Rush Mode properties
    public bool IsRushModeActive
    {
        get => _isRushModeActive;
        set => SetProperty(ref _isRushModeActive, value);
    }

    public bool ShowRushConfig
    {
        get => _showRushConfig;
        set => SetProperty(ref _showRushConfig, value);
    }

    public string RushAmountText
    {
        get => _rushAmountText;
        set => SetProperty(ref _rushAmountText, value);
    }

    public decimal RushAmount
    {
        get => _rushAmount;
        set => SetProperty(ref _rushAmount, value);
    }

    public bool ShowRushStatus
    {
        get => _showRushStatus;
        set => SetProperty(ref _showRushStatus, value);
    }

    public bool RushStatusSuccess
    {
        get => _rushStatusSuccess;
        set => SetProperty(ref _rushStatusSuccess, value);
    }

    public string RushStatusMessage
    {
        get => _rushStatusMessage;
        set => SetProperty(ref _rushStatusMessage, value);
    }

    public void StartScanning()
    {
        _hasProcessedCode = false;
        IsScanning = true;
        StatusMessage = "Scannez le QR code du client";
    }

    public void StopScanning()
    {
        IsScanning = false;
    }

    /// <summary>
    /// S'assure que la session est chargée et le token défini avant tout appel API
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        await _sessionService.LoadAsync().ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_sessionService.AuthToken))
        {
            _apis.SetBearerToken(_sessionService.AuthToken);
        }

        _isInitialized = true;
    }

    public async Task ProcessScannedCodeAsync(string qrCodeValue)
    {
        if (_hasProcessedCode || string.IsNullOrWhiteSpace(qrCodeValue))
            return;

        _hasProcessedCode = true;
        IsScanning = false;
        IsLoading = true;
        StatusMessage = "Vérification du QR code...";

        try
        {
            // S'assurer que le token est chargé AVANT l'appel API
            await EnsureInitializedAsync().ConfigureAwait(false);

            // Récupérer les infos fidélité du client via l'API
            var request = new GetLoyaltyByQrCodeRequest { QrCode = qrCodeValue };
            var response = await _apis
                .PostAsync<GetLoyaltyByQrCodeRequest, LoyaltyInfoResponse>(
                    "/api/mobile/getLoyaltyByQrCode", request)
                .ConfigureAwait(false);

            if (response?.Success != true || response.Data == null)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    StatusMessage = response?.Message ?? "QR code invalide";
                    await Task.Delay(2000);
                    _hasProcessedCode = false;
                    IsScanning = true;
                    StatusMessage = "Scannez le QR code du client";
                });
                return;
            }

            _currentClient = response.Data;

            // Afficher les informations du client
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsScanning = false;
                ShowClientInfo = true;
                ShowAmountForm = true;
                ClientName = _currentClient.DisplayName;
                ClientPoints = $"{_currentClient.Couronnes} couronnes";
                ClientPointsInEuros = $"{_currentClient.ValeurEnEuros:C}";
                StatusMessage = "Client identifié - Saisissez le montant";
            });
        }
        catch (HttpRequestException ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var errorMsg = ex.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "Session expirée. Veuillez vous reconnecter."
                    : "Erreur de connexion au serveur";

                StatusMessage = errorMsg;
                await Task.Delay(2000);
                _hasProcessedCode = false;
                IsScanning = true;
                StatusMessage = "Scannez le QR code du client";
            });
        }
        catch (Exception)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                StatusMessage = "Erreur lors de la lecture du QR code";
                await Task.Delay(2000);
                _hasProcessedCode = false;
                IsScanning = true;
                StatusMessage = "Scannez le QR code du client";
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ValidateAmountAsync()
    {
        if (string.IsNullOrWhiteSpace(AmountText))
        {
            await DialogService.DisplayAlertAsync("Erreur", "Veuillez saisir un montant.", "OK");
            return;
        }

        if (!decimal.TryParse(AmountText.Replace(",", "."), out _amountInEuros) || _amountInEuros <= 0)
        {
            await DialogService.DisplayAlertAsync("Erreur", "Veuillez saisir un montant valide.", "OK");
            return;
        }

        if (_currentClient == null)
        {
            await DialogService.DisplayAlertAsync("Erreur", "Aucun client identifié.", "OK");
            return;
        }

        // Calculer les points à ajouter : 1€ = 1 couronne
        _pointsToAdd = (int)Math.Round(_amountInEuros, MidpointRounding.AwayFromZero);

        var newBalance = _currentClient.Couronnes + _pointsToAdd;
        var newEuroValue = newBalance / 15m;

        ConfirmationMessage = $"Client : {_currentClient.DisplayName}\n\n" +
                             $"Solde actuel : {_currentClient.Couronnes} couronnes ({_currentClient.ValeurEnEuros:C})\n\n" +
                             $"Montant de l'achat : {_amountInEuros:C}\n" +
                             $"Points à ajouter : {_pointsToAdd} couronnes\n\n" +
                             $"Nouveau solde : {newBalance} couronnes ({newEuroValue:C})\n\n" +
                             $"Confirmer l'ajout des points ?";

        ShowAmountForm = false;
        ShowConfirmation = true;
    }

    private async Task AddPointsAsync()
    {
        if (_currentClient == null)
            return;

        IsLoading = true;
        StatusMessage = "Ajout des points en cours...";

        try
        {
            // S'assurer que le token est chargé
            await EnsureInitializedAsync().ConfigureAwait(false);

            // Appeler l'API pour ajouter les points
            // NOTE: Cette API doit être implémentée côté serveur
            var request = new AddPointsRequest
            {
                LoyaltyUserId = _currentClient.UserId,
                AmountInEuros = _amountInEuros,
                PointsToAdd = _pointsToAdd
            };

            // À décommenter quand l'API sera implémentée:
            var response = await _apis
                .PostAsync<AddPointsRequest, AddPointsResponse>(
                    "/api/mobile/addLoyaltyPoints", request)
                .ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (response?.Success == true)
                {
                    var message = $"Points ajoutés avec succès !\n\n" +
                                 $"Points ajoutés : {response.PointsAdded} couronnes\n" +
                                 $"Nouveau solde : {response.NewCouronnesBalance} couronnes ({response.NewEuroValue:C})";

                    await DialogService.DisplayAlertAsync("Succès", message, "OK");

                    // Réinitialiser pour une nouvelle transaction
                    StartNewTransaction();
                }
                else
                {
                    await DialogService.DisplayAlertAsync(
                        "Erreur",
                        response?.Message ?? "Impossible d'ajouter les points.",
                        "OK");
                    ShowConfirmation = false;
                    ShowAmountForm = true;
                }
            });

            /* Pour tester SANS l'API (à supprimer quand l'API sera implémentée):
            var newBalance = _currentClient.Couronnes + _pointsToAdd;
            var newEuroValue = newBalance / 15m;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var message = $"Points ajoutés avec succès !\n\n" +
                             $"Points ajoutés : {_pointsToAdd} couronnes\n" +
                             $"Nouveau solde : {newBalance} couronnes ({newEuroValue:C})";

                await DialogService.DisplayAlertAsync("Succès", message, "OK");
                StartNewTransaction();
            });
            */
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DialogService.DisplayAlertAsync(
                    "Erreur",
                    $"Erreur lors de l'ajout des points : {ex.Message}",
                    "OK");
                ShowConfirmation = false;
                ShowAmountForm = true;
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CancelAdd()
    {
        ShowConfirmation = false;
        ShowAmountForm = true;
        StatusMessage = "Saisissez le montant";
    }

    private void StartNewTransaction()
    {
        _currentClient = null;
        _hasProcessedCode = false;
        ShowClientInfo = false;
        ShowAmountForm = false;
        ShowConfirmation = false;
        ClientName = string.Empty;
        ClientPoints = string.Empty;
        ClientPointsInEuros = string.Empty;
        AmountText = string.Empty;
        ConfirmationMessage = string.Empty;
        IsScanning = true;
        StatusMessage = "Scannez le QR code du client";
    }

    private void ToggleTorch()
    {
        _isTorchOn = !_isTorchOn;
    }

    private async Task CancelAsync()
    {
        await NavigateBackAsync();
    }

    private static Task NavigateBackAsync()
    {
        if (Shell.Current != null)
        {
            return Shell.Current.GoToAsync("..");
        }
        return Task.CompletedTask;
    }

    // Rush Mode methods
    private void OpenRushConfig()
    {
        ShowRushConfig = true;
        RushAmountText = RushAmount > 0 ? RushAmount.ToString("F2") : string.Empty;
    }

    private void ValidateRushAmount()
    {
        if (string.IsNullOrWhiteSpace(RushAmountText))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DialogService.DisplayAlertAsync("Erreur", "Veuillez saisir un montant.", "OK");
            });
            return;
        }

        if (!decimal.TryParse(RushAmountText.Replace(",", "."), out var amount) || amount <= 0)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DialogService.DisplayAlertAsync("Erreur", "Veuillez saisir un montant valide.", "OK");
            });
            return;
        }

        RushAmount = amount;
        IsRushModeActive = true;
        ShowRushConfig = false;
        StatusMessage = $"Mode Rush activé - {RushAmount:C} par scan";
    }

    private void CloseRushConfig()
    {
        ShowRushConfig = false;
    }

    private void DeactivateRushMode()
    {
        IsRushModeActive = false;
        RushAmount = 0;
        RushAmountText = string.Empty;
        StatusMessage = "Scannez le QR code du client";
    }

    private void HideRushStatus()
    {
        ShowRushStatus = false;
    }

    /// <summary>
    /// Traite le scan en mode Rush - ajout direct des points sans confirmation
    /// </summary>
    public async Task ProcessRushScanAsync(string qrCodeValue)
    {
        if (_hasProcessedCode || string.IsNullOrWhiteSpace(qrCodeValue))
            return;

        _hasProcessedCode = true;
        IsLoading = true;
        ShowRushStatus = false;

        try
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            // 1. Récupérer les infos du client
            var getRequest = new GetLoyaltyByQrCodeRequest { QrCode = qrCodeValue };
            var getResponse = await _apis
                .PostAsync<GetLoyaltyByQrCodeRequest, LoyaltyInfoResponse>(
                    "/api/mobile/getLoyaltyByQrCode", getRequest)
                .ConfigureAwait(false);

            if (getResponse?.Success != true || getResponse.Data == null)
            {
                await ShowRushStatusAsync(false, getResponse?.Message ?? "Client non trouvé");
                return;
            }

            var client = getResponse.Data;
            var pointsToAdd = (int)Math.Round(RushAmount, MidpointRounding.AwayFromZero);

            // 2. Ajouter directement les points
            var addRequest = new AddPointsRequest
            {
                LoyaltyUserId = client.UserId,
                AmountInEuros = RushAmount,
                PointsToAdd = pointsToAdd
            };

            var addResponse = await _apis
                .PostAsync<AddPointsRequest, AddPointsResponse>(
                    "/api/mobile/addLoyaltyPoints", addRequest)
                .ConfigureAwait(false);

            if (addResponse?.Success == true)
            {
                await ShowRushStatusAsync(true, $"{client.DisplayName}\n+{addResponse.PointsAdded} couronnes");
            }
            else
            {
                await ShowRushStatusAsync(false, addResponse?.Message ?? "Erreur d'ajout");
            }
        }
        catch (HttpRequestException ex)
        {
            var errorMsg = ex.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? "Session expirée"
                : "Erreur serveur";
            await ShowRushStatusAsync(false, errorMsg);
        }
        catch (Exception)
        {
            await ShowRushStatusAsync(false, "Erreur inattendue");
        }
        finally
        {
            IsLoading = false;
            // Réactiver le scan après un court délai
            await Task.Delay(1500);
            _hasProcessedCode = false;
        }
    }

    private async Task ShowRushStatusAsync(bool success, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            RushStatusSuccess = success;
            RushStatusMessage = message;
            ShowRushStatus = true;
        });

        // Auto-masquer après 2 secondes
        await Task.Delay(2000);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ShowRushStatus = false;
        });
    }
}
