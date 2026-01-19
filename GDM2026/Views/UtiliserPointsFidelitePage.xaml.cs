using GDM2026.ViewModels;
using ZXing.Net.Maui;

namespace GDM2026.Views;

public partial class UtiliserPointsFidelitePage : ContentPage
{
    private readonly UtiliserPointsFideliteViewModel _viewModel;

    public UtiliserPointsFidelitePage()
    {
        InitializeComponent();
        _viewModel = (UtiliserPointsFideliteViewModel)BindingContext;

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
            await DisplayAlert("Permission requise",
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
            // Arrêter le scan immédiatement
            BarcodeReader.IsDetecting = false;

            // Traiter le résultat sur le thread UI
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await _viewModel.ProcessScannedCodeAsync(result.Value);
            });
        }
    }

    private void OnTorchClicked(object? sender, EventArgs e)
    {
        BarcodeReader.IsTorchOn = !BarcodeReader.IsTorchOn;
        TorchButton.Text = BarcodeReader.IsTorchOn ? "Torche ON" : "Torche";
    }
}
