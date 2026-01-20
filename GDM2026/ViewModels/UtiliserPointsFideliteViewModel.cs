using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Net.Http;
using System.Windows.Input;
using ZXing.Net.Maui;

namespace GDM2026.ViewModels;

public class UtiliserPointsFideliteViewModel : BaseViewModel
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
    private bool _showPaymentForm;

    private LoyaltyInfo? _currentClient;
    private string _clientName = string.Empty;
    private string _clientPoints = string.Empty;
    private string _clientPointsInEuros = string.Empty;
    private decimal _amountToPay;
    private string _amountToPayText = string.Empty;
    private decimal _maxUsableInEuros;
    private int _maxUsableInCouronnes;
    private decimal _remainingAmount;
    private string _confirmationMessage = string.Empty;
    private bool _showConfirmation;

    public UtiliserPointsFideliteViewModel()
    {
        CancelCommand = new Command(async () => await CancelAsync());
        ToggleTorchCommand = new Command(ToggleTorch);
        ValidateAmountCommand = new Command(async () => await ValidateAmountAsync());
        UsePointsCommand = new Command(async () => await UsePointsAsync());
        DontUsePointsCommand = new Command(async () => await DontUsePointsAsync());
        StartNewTransactionCommand = new Command(StartNewTransaction);

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
    public ICommand UsePointsCommand { get; }
    public ICommand DontUsePointsCommand { get; }
    public ICommand StartNewTransactionCommand { get; }

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

    public bool ShowPaymentForm
    {
        get => _showPaymentForm;
        set => SetProperty(ref _showPaymentForm, value);
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

    public string AmountToPayText
    {
        get => _amountToPayText;
        set => SetProperty(ref _amountToPayText, value);
    }

    public string ConfirmationMessage
    {
        get => _confirmationMessage;
        set => SetProperty(ref _confirmationMessage, value);
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
                ShowClientInfo = true;
                ShowPaymentForm = true;
                ClientName = _currentClient.DisplayName;
                ClientPoints = $"{_currentClient.Couronnes} couronnes";
                ClientPointsInEuros = $"{_currentClient.ValeurEnEuros:C}";
                StatusMessage = "Client identifié";
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
        if (string.IsNullOrWhiteSpace(AmountToPayText))
        {
            await DialogService.DisplayAlertAsync("Erreur", "Veuillez saisir un montant à payer.", "OK");
            return;
        }

        if (!decimal.TryParse(AmountToPayText.Replace(",", "."), out _amountToPay) || _amountToPay <= 0)
        {
            await DialogService.DisplayAlertAsync("Erreur", "Veuillez saisir un montant valide.", "OK");
            return;
        }

        if (_currentClient == null || _currentClient.Couronnes <= 0)
        {
            await DialogService.DisplayAlertAsync(
                "Pas de points",
                "Ce client n'a pas de points fidélité disponibles.",
                "OK");
            return;
        }

        // Calculer le maximum utilisable
        _maxUsableInEuros = Math.Min(_currentClient.ValeurEnEuros, _amountToPay);
        _maxUsableInCouronnes = (int)(_maxUsableInEuros / 0.01m);

        // Si le montant à payer est couvert par les points
        if (_maxUsableInEuros >= _amountToPay)
        {
            _remainingAmount = 0;
            ConfirmationMessage = $"Le client peut utiliser {_maxUsableInCouronnes} couronnes ({_maxUsableInEuros:C}) pour payer la totalité.\n\n" +
                                 $"Montant à payer : {_amountToPay:C}\n" +
                                 $"Montant couvert par les points : {_maxUsableInEuros:C}\n" +
                                 $"Montant restant : {_remainingAmount:C}\n\n" +
                                 $"Voulez-vous utiliser les points fidélité ?";
        }
        else
        {
            _remainingAmount = _amountToPay - _maxUsableInEuros;
            ConfirmationMessage = $"Le client peut utiliser {_maxUsableInCouronnes} couronnes ({_maxUsableInEuros:C}).\n\n" +
                                 $"Montant à payer : {_amountToPay:C}\n" +
                                 $"Montant couvert par les points : {_maxUsableInEuros:C}\n" +
                                 $"Montant restant : {_remainingAmount:C}\n\n" +
                                 $"Voulez-vous utiliser les points fidélité ?";
        }

        ShowPaymentForm = false;
        ShowConfirmation = true;
    }

    private async Task UsePointsAsync()
    {
        if (_currentClient == null)
            return;

        IsLoading = true;
        StatusMessage = "Mise à jour des points fidélité...";

        try
        {
            // S'assurer que le token est chargé
            await EnsureInitializedAsync().ConfigureAwait(false);

            // Appeler la future API pour mettre à jour les points
            // NOTE: Cette API n'existe pas encore, elle devra être implémentée côté serveur
            var request = new UsePointsRequest
            {
                LoyaltyUserId = _currentClient.UserId,
                AmountInEuros = _maxUsableInEuros,
                CouronnesUsed = _maxUsableInCouronnes
            };

            // Pour l'instant, on simule une réponse réussie
            // À remplacer par l'appel API réel quand il sera implémenté:
            // var response = await _apis.PostAsync<UsePointsRequest, UsePointsResponse>(
            //     "/api/mobile/usePoints", request).ConfigureAwait(false);

            var newBalance = _currentClient.Couronnes - _maxUsableInCouronnes;
            var newEuroValue = newBalance * 0.01m;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var message = $"Points fidélité utilisés avec succès !\n\n" +
                             $"Couronnes utilisées : {_maxUsableInCouronnes}\n" +
                             $"Montant déduit : {_maxUsableInEuros:C}\n" +
                             $"Nouveau solde : {newBalance} couronnes ({newEuroValue:C})";

                if (_remainingAmount > 0)
                {
                    message += $"\n\nMontant restant à payer : {_remainingAmount:C}";
                }

                await DialogService.DisplayAlertAsync("Succès", message, "OK");

                // Réinitialiser pour une nouvelle transaction
                StartNewTransaction();
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DialogService.DisplayAlertAsync(
                    "Erreur",
                    $"Erreur lors de la mise à jour des points : {ex.Message}",
                    "OK");
                ShowConfirmation = false;
                ShowPaymentForm = true;
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DontUsePointsAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DialogService.DisplayAlertAsync(
                "Transaction",
                $"Montant total à payer : {_amountToPay:C}\n(Points fidélité non utilisés)",
                "OK");

            // Réinitialiser pour une nouvelle transaction
            StartNewTransaction();
        });
    }

    private void StartNewTransaction()
    {
        _currentClient = null;
        _hasProcessedCode = false;
        ShowClientInfo = false;
        ShowPaymentForm = false;
        ShowConfirmation = false;
        ClientName = string.Empty;
        ClientPoints = string.Empty;
        ClientPointsInEuros = string.Empty;
        AmountToPayText = string.Empty;
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
}
