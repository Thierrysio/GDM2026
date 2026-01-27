using ZXing.Net.Maui;

namespace GDM2026.Views;

public partial class OrderQrScannerPage : ContentPage
{
    private bool _hasProcessed;

    /// <summary>
    /// Événement déclenché quand un QR code de commande valide est scanné.
    /// Le paramètre int est l'ID de la commande extraite du format "commande-XXX".
    /// </summary>
    public event EventHandler<int>? OrderScanned;

    public OrderQrScannerPage()
    {
        InitializeComponent();
        ConfigureScanner();
    }

    private void ConfigureScanner()
    {
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

        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (status == PermissionStatus.Granted)
        {
            _hasProcessed = false;
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
        BarcodeReader.IsDetecting = false;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var result = e.Results?.FirstOrDefault();
        if (result == null || string.IsNullOrWhiteSpace(result.Value) || _hasProcessed)
            return;

        _hasProcessed = true;
        BarcodeReader.IsDetecting = false;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ProcessScannedValueAsync(result.Value);
        });
    }

    private async Task ProcessScannedValueAsync(string value)
    {
        StatusLabel.Text = "Vérification...";
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;

        // Format attendu : "commande-XXX" où XXX est l'ID numérique
        var trimmed = value.Trim();

        if (trimmed.StartsWith("commande-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(trimmed.AsSpan("commande-".Length), out var orderId)
            && orderId > 0)
        {
            StatusLabel.Text = $"Commande #{orderId} trouvée !";
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;

            OrderScanned?.Invoke(this, orderId);
            await Navigation.PopAsync();
        }
        else
        {
            StatusLabel.Text = "QR code invalide. Format attendu : commande-XXX";
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;

            await Task.Delay(2000);
            _hasProcessed = false;
            BarcodeReader.IsDetecting = true;
            StatusLabel.Text = "En attente du scan...";
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void OnTorchClicked(object? sender, EventArgs e)
    {
        BarcodeReader.IsTorchOn = !BarcodeReader.IsTorchOn;
        TorchButton.Text = BarcodeReader.IsTorchOn ? "Torche ON" : "Torche";
    }
}
