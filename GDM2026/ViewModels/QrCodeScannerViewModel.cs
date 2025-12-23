using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Net.Http;
using System.Windows.Input;
using ZXing.Net.Maui;

namespace GDM2026.ViewModels;

public class QrCodeScannerViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _isScanning = true;
    private bool _isLoading;
    private bool _isTorchOn;
    private string _statusMessage = "En attente du scan...";
    private bool _hasProcessedCode;
    private bool _isInitialized;

    private readonly int _orderId;
    private readonly double _maxReduction;

    // Événement pour notifier quand une réduction est appliquée
    public event EventHandler<ApplyLoyaltyResponse>? LoyaltyApplied;

    public QrCodeScannerViewModel() : this(0, 0) { }

    public QrCodeScannerViewModel(int orderId, double maxReduction)
    {
        _orderId = orderId;
        _maxReduction = maxReduction;

        CancelCommand = new Command(async () => await CancelAsync());
        ToggleTorchCommand = new Command(ToggleTorch);

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

    public void StartScanning()
    {
        _hasProcessedCode = false;
        IsScanning = true;
        StatusMessage = "En attente du scan...";
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
                    StatusMessage = "En attente du scan...";
                });
                return;
            }

            var loyaltyInfo = response.Data;

            // Afficher le popup pour choisir les points à utiliser
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await ShowLoyaltySelectionAsync(loyaltyInfo);
            });
        }
        catch (HttpRequestException ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Afficher plus de détails sur l'erreur pour le debug
                var errorMsg = ex.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "Session expirée. Veuillez vous reconnecter."
                    : "Erreur de connexion au serveur";
                    
                StatusMessage = errorMsg;
                await Task.Delay(2000);
                _hasProcessedCode = false;
                IsScanning = true;
                StatusMessage = "En attente du scan...";
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
                StatusMessage = "En attente du scan...";
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ShowLoyaltySelectionAsync(LoyaltyInfo loyaltyInfo)
    {
        if (loyaltyInfo.Couronnes <= 0)
        {
            await DialogService.DisplayAlertAsync(
                "Pas de points",
                $"{loyaltyInfo.DisplayName} n'a pas de points fidélité disponibles.",
                "OK");
            await NavigateBackAsync();
            return;
        }

        // Calculer le maximum utilisable (limité par le montant de la commande)
        var maxCouronnesParMontant = (int)(_maxReduction / 0.01); // Conversion euros -> couronnes
        var maxCouronnesUtilisables = Math.Min(loyaltyInfo.Couronnes, maxCouronnesParMontant);
        var maxReductionPossible = maxCouronnesUtilisables * 0.01;

        var message = $"Client : {loyaltyInfo.DisplayName}\n" +
                      $"Points disponibles : {loyaltyInfo.Couronnes} couronnes\n" +
                      $"Valeur : {loyaltyInfo.ValeurEnEuros:C}\n\n" +
                      $"Maximum utilisable sur cette commande :\n" +
                      $"{maxCouronnesUtilisables} couronnes = {maxReductionPossible:C}\n\n" +
                      $"Combien de couronnes utiliser ?";

        var result = await Application.Current!.Windows[0].Page!.DisplayPromptAsync(
            "Points Fidélité",
            message,
            "Appliquer",
            "Annuler",
            placeholder: $"Max: {maxCouronnesUtilisables}",
            maxLength: 5,
            keyboard: Keyboard.Numeric,
            initialValue: maxCouronnesUtilisables.ToString());

        if (string.IsNullOrWhiteSpace(result))
        {
            await NavigateBackAsync();
            return;
        }

        if (!int.TryParse(result, out var couronnesAUtiliser) || couronnesAUtiliser <= 0)
        {
            await DialogService.DisplayAlertAsync("Erreur", "Veuillez entrer un nombre valide.", "OK");
            await NavigateBackAsync();
            return;
        }

        if (couronnesAUtiliser > maxCouronnesUtilisables)
        {
            await DialogService.DisplayAlertAsync(
                "Erreur",
                $"Vous ne pouvez utiliser que {maxCouronnesUtilisables} couronnes maximum.",
                "OK");
            await NavigateBackAsync();
            return;
        }

        // Appliquer la réduction
        await ApplyLoyaltyReductionAsync(loyaltyInfo.UserId, couronnesAUtiliser);
    }

    private async Task ApplyLoyaltyReductionAsync(int userId, int couronnes)
    {
        IsLoading = true;
        StatusMessage = "Application de la réduction...";

        try
        {
            // S'assurer que le token est chargé
            await EnsureInitializedAsync().ConfigureAwait(false);

            var reductionMontant = couronnes * 0.01;
            var request = new ApplyLoyaltyRequest
            {
                CommandeId = _orderId,
                UserId = userId,
                CouronnesUtilisees = couronnes,
                MontantReduction = reductionMontant
            };

            var response = await _apis
                .PostAsync<ApplyLoyaltyRequest, ApplyLoyaltyResponse>(
                    "/api/mobile/applyLoyaltyReduction", request)
                .ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (response?.Success == true)
                {
                    await DialogService.DisplayAlertAsync(
                        "Succès",
                        $"Réduction de {response.ReductionAppliquee:C} appliquée !\n" +
                        $"Nouveau total : {response.NouveauMontantCommande:C}\n" +
                        $"Nouveau solde client : {response.NouveauSoldeCouronnes} couronnes",
                        "OK");

                    // Notifier via l'événement
                    LoyaltyApplied?.Invoke(this, response);
                }
                else
                {
                    await DialogService.DisplayAlertAsync(
                        "Erreur",
                        response?.Message ?? "Impossible d'appliquer la réduction.",
                        "OK");
                }

                await NavigateBackAsync();
            });
        }
        catch (Exception)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DialogService.DisplayAlertAsync(
                    "Erreur",
                    "Erreur lors de l'application de la réduction.",
                    "OK");
                await NavigateBackAsync();
            });
        }
        finally
        {
            IsLoading = false;
        }
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
