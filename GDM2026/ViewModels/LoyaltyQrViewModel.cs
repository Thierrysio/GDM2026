using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Threading.Tasks;

namespace GDM2026.ViewModels;

public class LoyaltyQrViewModel : BaseViewModel
{
    private readonly SessionService _sessionService = new();
    private string _qrImageUrl = string.Empty;
    private string _accountLabel = "Compte fidélité";
    private string _description = "Scannez ce code pour identifier votre compte Dantec Market.";
    private string _euroHint = "Les euros cumulés à partir de vos couronnes seront associés à ce compte.";

    public string QrImageUrl
    {
        get => _qrImageUrl;
        set => SetProperty(ref _qrImageUrl, value);
    }

    public string AccountLabel
    {
        get => _accountLabel;
        set => SetProperty(ref _accountLabel, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string EuroHint
    {
        get => _euroHint;
        set => SetProperty(ref _euroHint, value);
    }

    public async Task InitializeAsync()
    {
        await _sessionService.LoadAsync().ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var user = _sessionService.CurrentUser;
            AccountLabel = user?.DisplayName ?? "Compte invité";

            var qrPayload = BuildQrPayload(user);
            QrImageUrl = BuildQrUrl(qrPayload);

            EuroHint = "Présentez ce QR code pour utiliser automatiquement les euros issus de vos couronnes.";
            Description = "Votre QR code d'identification Dantec Market.";
        });
    }

    private static string BuildQrPayload(User? user)
    {
        if (user == null)
        {
            return "dantecmarket:utilisateur:invite";
        }

        var identifier = !string.IsNullOrWhiteSpace(user.UserIdentifier)
            ? user.UserIdentifier.Trim()
            : user.Email ?? user.Id.ToString();

        return $"dantecmarket:utilisateur:{identifier}";
    }

    private static string BuildQrUrl(string payload)
    {
        var encoded = Uri.EscapeDataString(payload);
        return $"https://api.qrserver.com/v1/create-qr-code/?size=480x480&data={encoded}";
    }
}
