using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Net.Http;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class MainPageViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _rememberMe;
    private bool _isCheckingSession;
    private string _feedbackMessage = string.Empty;
    private Color _feedbackColor = Colors.LightGray;

    public MainPageViewModel()
    {
        LoginCommand = new Command(async () => await LoginAsync(), () => !IsBusy);
    }

    public ICommand LoginCommand { get; }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    public string FeedbackMessage
    {
        get => _feedbackMessage;
        set => SetProperty(ref _feedbackMessage, value);
    }

    public Color FeedbackColor
    {
        get => _feedbackColor;
        set => SetProperty(ref _feedbackColor, value);
    }

    public async Task InitializeAsync()
    {
        if (_isCheckingSession)
        {
            return;
        }

        _isCheckingSession = true;

        try
        {
            var hasSession = await _sessionService.LoadAsync().ConfigureAwait(false);
            if (hasSession && _sessionService.IsAuthenticated)
            {
                await NavigateToHomeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _isCheckingSession = false;
        }
    }

    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            FeedbackColor = Colors.OrangeRed;
            FeedbackMessage = "Merci de renseigner un identifiant et un mot de passe.";
            return;
        }

        IsBusy = true;
        ((Command)LoginCommand).ChangeCanExecute();
        FeedbackColor = Colors.LightGray;
        FeedbackMessage = "Vérification de vos identifiants...";

        try
        {
            var user = await AuthenticateAsync(Username, Password).ConfigureAwait(false);

            if (user != null)
            {
                await _sessionService.SaveAsync(user, user.Token).ConfigureAwait(false);

                var greeting = $"Bienvenue {(user.Prenom ?? user.Nom ?? user.UserIdentifier ?? Username)}".Trim();
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    FeedbackColor = Colors.LimeGreen;
                    FeedbackMessage = greeting;
                    await DialogService.DisplayAlertAsync("Connexion réussie", "Authentification validée.", "Continuer");
                });

                await NavigateToHomeAsync().ConfigureAwait(false);
            }
            else
            {
                await ShowAuthenticationErrorAsync("Email ou mot de passe incorrect.").ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            await ShowAuthenticationErrorAsync("Délai d'attente dépassé. Vérifiez votre connexion internet.").ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            await ShowAuthenticationErrorAsync("Impossible de contacter le serveur. Veuillez réessayer.").ConfigureAwait(false);
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsBusy = false;
                ((Command)LoginCommand).ChangeCanExecute();
            });
        }
    }

    private async Task<User?> AuthenticateAsync(string username, string password)
    {
        var loginData = new
        {
            Email = username,
            Password = password
        };

        try
        {
            return await _apis
                .PostAsync<object, User>("https://dantecmarket.com/api/mobile/GetFindUser", loginData)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.Message.StartsWith("API error", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    private async Task ShowAuthenticationErrorAsync(string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            FeedbackColor = Colors.OrangeRed;
            FeedbackMessage = message;
            await DialogService.DisplayAlertAsync("Erreur", "Impossible de vous connecter avec ces identifiants.", "Réessayer");
        });
    }

    private static Task NavigateToHomeAsync()
    {
        if (Shell.Current == null)
        {
            return Task.CompletedTask;
        }

        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync($"//{nameof(HomePage)}").ConfigureAwait(false);
            }
        });
    }
}
