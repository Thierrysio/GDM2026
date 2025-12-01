using GDM2026.Models;
using GDM2026.Services;
using System;
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
                        FeedbackLabel.Text = $"Bienvenue {(user.Nom ?? user.UserIdentifier ?? username)}";
                        await DisplayAlert("Connexion réussie", "Authentification validée via getfinduser.", "Continuer");
                        await NavigateToHomeAsync();
                    });
                }
                else
                {
                    await ShowAuthenticationErrorAsync();
                }
            }
            catch (Exception)
            {
                await ShowAuthenticationErrorAsync();
            }
            finally
            {
                LoginButton.IsEnabled = true;
            }
        }

        private async Task<User?> AuthenticateAsync(string username, string password)
        {
            var relativeUrl = $"getfinduser?user={Uri.EscapeDataString(username)}&pass={Uri.EscapeDataString(password)}";
            return await _apis.GetAsync<User>(relativeUrl).ConfigureAwait(false);
        }

        private async Task ShowAuthenticationErrorAsync()
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                FeedbackLabel.TextColor = Colors.OrangeRed;
                FeedbackLabel.Text = "Identifiant ou mot de passe incorrect.";
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
