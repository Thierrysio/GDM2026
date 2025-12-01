using GestDM.Services;
using GDM2026.Models;
using GDM2026.Services;
using System;
using System.Linq;

namespace GDM2026
{
    public partial class MainPage : ContentPage, IDisposable
    {
        private readonly IApis _apis;
        private readonly ISessionService _sessionService;
        private bool _disposed;

        public MainPage()
        {
            InitializeComponent();

            _apis = new Apis();
            _sessionService = new SessionService();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _apis.Dispose();
            _disposed = true;
        }

        private async void OnLoginClicked(object? sender, EventArgs e)
        {
            FeedbackLabel.Text = string.Empty;

            var username = UsernameEntry.Text?.Trim() ?? string.Empty;
            var password = PasswordEntry.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                FeedbackLabel.TextColor = Colors.OrangeRed;
                FeedbackLabel.Text = "Veuillez saisir un identifiant et un mot de passe.";
                return;
            }

            try
            {
                var user = await _apis.GetFindUserAsync(username, password);

                if (IsAdminUser(user))
                {
                    FeedbackLabel.TextColor = Colors.LimeGreen;
                    FeedbackLabel.Text = "Connexion réussie. Accès administrateur accordé.";

                    if (RememberMeCheckBox.IsChecked)
                    {
                        await _sessionService.SaveAsync(user!, user?.Token ?? string.Empty);
                    }
                    else
                    {
                        await _sessionService.ClearAsync();
                    }

                    await DisplayAlert("Bienvenue", "Vous êtes connecté en tant qu'administrateur.", "Continuer");
                }
                else
                {
                    FeedbackLabel.TextColor = Colors.OrangeRed;
                    FeedbackLabel.Text = "Identifiants invalides ou droits insuffisants.";
                    await DisplayAlert("Erreur", "Seuls les administrateurs avec des identifiants valides peuvent continuer.", "Réessayer");
                }
            }
            catch (Exception ex)
            {
                FeedbackLabel.TextColor = Colors.OrangeRed;
                FeedbackLabel.Text = "Une erreur est survenue pendant la connexion.";
                await DisplayAlert("Erreur", ex.Message, "Fermer");
            }
        }

        private static bool IsAdminUser(User? user)
        {
            return user != null && user.Roles.Any(role => role.Equals("admin", StringComparison.OrdinalIgnoreCase));
        }
    }
}
