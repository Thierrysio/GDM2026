using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace GDM2026.Views;

public partial class SplashPage : ContentPage
{
    private readonly SessionService _sessionService = new();
    private bool _navigated;

    public SplashPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_navigated)
        {
            return;
        }

        _navigated = true;
        await CheckSessionAsync();
    }

    private async Task CheckSessionAsync()
    {
        try
        {
            var hasSession = await _sessionService.LoadAsync().ConfigureAwait(false);

            var targetRoute = hasSession && _sessionService.IsAuthenticated
                ? $"//{nameof(HomePage)}"
                : $"//{nameof(MainPage)}";

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync(targetRoute);
                }
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("Erreur", "Impossible de vérifier votre session. Merci de réessayer.", "OK");
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
                }
            });
        }
    }
}
