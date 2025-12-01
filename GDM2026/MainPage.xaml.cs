namespace GDM2026
{
    public partial class MainPage : ContentPage
    {
        private const string AdminUsername = "admin";
        private const string AdminPassword = "GDM2026!";

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnLoginClicked(object? sender, EventArgs e)
        {
            var username = UsernameEntry.Text?.Trim() ?? string.Empty;
            var password = PasswordEntry.Text ?? string.Empty;

            if (IsAdminCredentials(username, password))
            {
                FeedbackLabel.TextColor = Colors.LimeGreen;
                FeedbackLabel.Text = "Connexion réussie. Accès administrateur accordé.";
                await DisplayAlert("Bienvenue", "Vous êtes connecté en tant qu'administrateur.", "Continuer");
            }
            else
            {
                FeedbackLabel.TextColor = Colors.OrangeRed;
                FeedbackLabel.Text = "Identifiants invalides. Accès refusé.";
                await DisplayAlert("Erreur", "Seuls les administrateurs avec des identifiants valides peuvent continuer.", "Réessayer");
            }
        }

        private static bool IsAdminCredentials(string username, string password)
        {
            return username.Equals(AdminUsername, StringComparison.OrdinalIgnoreCase)
                && password == AdminPassword;
        }
    }
}
