using GDM2026.ViewModels;
using ZXing.Net.Maui;

namespace GDM2026.Views;

public partial class AjouterPointsFidelitePage : ContentPage
{
    private readonly AjouterPointsFideliteViewModel _viewModel;

    public AjouterPointsFidelitePage()
    {
        InitializeComponent();
        _viewModel = (AjouterPointsFideliteViewModel)BindingContext;

        ConfigureScanner();
    }

    private void ConfigureScanner()
    {
        // Configuration du scanner
        BarcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false,
            TryHarder = true
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Demander la permission caméra si nécessaire
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (status == PermissionStatus.Granted)
        {
            _viewModel.StartScanning();
            BarcodeReader.IsDetecting = true;
        }
        else
        {
            await DisplayAlertAsync("Permission requise",
                "L'accès à la caméra est nécessaire pour scanner les QR codes.",
                "OK");
            await Navigation.PopAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopScanning();
        BarcodeReader.IsDetecting = false;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var result = e.Results?.FirstOrDefault();
        if (result != null && !string.IsNullOrWhiteSpace(result.Value))
        {
            // En mode Rush, ne pas arrêter le scan définitivement
            if (!_viewModel.IsRushModeActive)
            {
                BarcodeReader.IsDetecting = false;
            }

            // Traiter le résultat sur le thread UI
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_viewModel.IsRushModeActive)
                {
                    // Mode Rush : ajout direct des points
                    await _viewModel.ProcessRushScanAsync(result.Value);
                }
                else
                {
                    // Mode normal : workflow complet avec confirmation
                    await _viewModel.ProcessScannedCodeAsync(result.Value);
                }
            });
        }
    }

    private void OnTorchClicked(object? sender, EventArgs e)
    {
        BarcodeReader.IsTorchOn = !BarcodeReader.IsTorchOn;
        TorchButton.Text = BarcodeReader.IsTorchOn ? "Torche ON" : "Torche";
    }
}
