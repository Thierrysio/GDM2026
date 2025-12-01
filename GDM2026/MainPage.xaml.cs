using GDM2026.Models;
using GDM2026.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace GDM2026
{
    public partial class MainPage : ContentPage
    {
        private readonly Apis _apis = new();
        private readonly SessionService _sessionService = new();
        private bool _isCheckingSession;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_isCheckingSession)
            {
                return;
            }

            _isCheckingSession = true;

            try
            {
                var hasSession = await _sessionService.LoadAsync();
                if (hasSession && _sessionService.IsAuthenticated)
                {
                    await NavigateToHomeAsync();
                }
            }
            finally
            {
                _isCheckingSession = false;
            }
        }

        private async void OnLoginClicked(object? sender, EventArgs e)
        {
            var username = UsernameEntry.Text?.Trim() ?? string.Empty;
            var password = PasswordEntry.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                FeedbackLabel.TextColor = Colors.OrangeRed;
                FeedbackLabel.Text = "Merci de renseigner un identifiant et un mot de passe.";
                return;
            }

            LoginButton.IsEnabled = false;
            FeedbackLabel.TextColor = Colors.LightGray;
            FeedbackLabel.Text = "Vérification de vos identifiants...";

            try
            {
                var user = await AuthenticateAsync(username, password).ConfigureAwait(false);

                if (user != null)
                {
                    await _sessionService.SaveAsync(user, user.Token).ConfigureAwait(false);

                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        FeedbackLabel.TextColor = Colors.LimeGreen;
                        FeedbackLabel.Text = $"Bienvenue {(user.Prenom ?? user.Nom ?? user.UserIdentifier ?? username)}".Trim();
                        await DisplayAlert("Connexion réussie", "Authentification validée.", "Continuer");
                        await NavigateToHomeAsync();
                    });
                }
                else
                {
                    await ShowAuthenticationErrorAsync("Email ou mot de passe incorrect.");
                }
            }
            catch (TaskCanceledException)
            {
                await ShowAuthenticationErrorAsync("Délai d'attente dépassé. Vérifiez votre connexion internet.");
            }
            catch (HttpRequestException)
            {
                await ShowAuthenticationErrorAsync("Impossible de contacter le serveur. Veuillez réessayer.");
            }
            finally
            {
                LoginButton.IsEnabled = true;
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
                    .PostAsync<object, User>("/api/mobile/getFindUser", loginData)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.Message.StartsWith("API error", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        private async Task ShowAuthenticationErrorAsync(string message = "Identifiant ou mot de passe incorrect.")
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                FeedbackLabel.TextColor = Colors.OrangeRed;
                FeedbackLabel.Text = message;
                await DisplayAlert("Erreur", "Impossible de vous connecter avec ces identifiants.", "Réessayer");
            });
        }

        private static async Task NavigateToHomeAsync()
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync(nameof(HomePage));
            }
        }
    }
}
