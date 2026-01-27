using GDM2026.ViewModels;
using ZXing.Net.Maui;

namespace GDM2026.Views;

public partial class QrCodeScannerPage : ContentPage
{
    private readonly QrCodeScannerViewModel _viewModel;

    public QrCodeScannerPage()
    {
        InitializeComponent();
        _viewModel = new QrCodeScannerViewModel();
        BindingContext = _viewModel;
        
        ConfigureScanner();
    }

    public QrCodeScannerPage(int orderId, double maxReduction)
    {
        InitializeComponent();
        _viewModel = new QrCodeScannerViewModel(orderId, maxReduction);
        BindingContext = _viewModel;
        
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
        
        // Demander la permission cam�ra si n�cessaire
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
                "L'acc�s � la cam�ra est n�cessaire pour scanner les QR codes.", 
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
            // Arr�ter le scan imm�diatement
            BarcodeReader.IsDetecting = false;
            
            // Traiter le r�sultat sur le thread UI
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
