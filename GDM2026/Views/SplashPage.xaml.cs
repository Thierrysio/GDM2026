using GDM2026.Services;
using GDM2026;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using System;

namespace GDM2026.Views;

public partial class SplashPage : ContentPage
{
    private readonly SessionService _sessionService = new();
    private readonly string _loginRoute = $"//{nameof(MainPage)}";
    private readonly string _homeRoute = $"//{nameof(HomePage)}";
    private readonly TimeSpan _sessionCheckTimeout = TimeSpan.FromSeconds(3);
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
        var targetRoute = _loginRoute;

        try
        {
            var loadSessionTask = _sessionService.LoadAsync();
            var completedTask = await Task.WhenAny(loadSessionTask, Task.Delay(_sessionCheckTimeout)).ConfigureAwait(false);

            if (completedTask != loadSessionTask)
            {
                await NavigateSafelyAsync(targetRoute).ConfigureAwait(false);
                return;
            }

            var hasSession = await loadSessionTask.ConfigureAwait(false);
            if (hasSession && _sessionService.IsAuthenticated)
            {
                targetRoute = _homeRoute;
            }
        }
        catch
        {
            await ShowErrorAndFallbackAsync();
            return;
        }

        await NavigateSafelyAsync(targetRoute);
    }

    private async Task NavigateSafelyAsync(string route)
    {
        if (Shell.Current?.Dispatcher is null)
        {
            return;
        }

        try
        {
            await Shell.Current.Dispatcher.DispatchAsync(async () =>
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync(route, animate: false);
                }
            });
        }
        catch
        {
            await Shell.Current.Dispatcher.DispatchAsync(async () =>
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync(_loginRoute, animate: false);
                }
            });
        }
    }

    private async Task ShowErrorAndFallbackAsync()
    {
        if (Shell.Current?.Dispatcher is null)
        {
            return;
        }

        await Shell.Current.Dispatcher.DispatchAsync(async () =>
        {
            if (Shell.Current == null)
            {
                return;
            }

            await DisplayAlertAsync("Erreur", "Impossible de vérifier votre session. Merci de réessayer.", "OK");
            await Shell.Current.GoToAsync(_loginRoute, animate: false);
        });
    }
}
