using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace GDM2026
{
    public partial class HomePage : ContentPage
    {
        private readonly SessionService _sessionService = new();

        public ObservableCollection<CategoryCard> Categories { get; } = new();

        public HomePage()
        {
            InitializeComponent();
            BindingContext = this;
            LoadCategories();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await LoadSessionAsync().ConfigureAwait(false);
        }

        private async Task LoadSessionAsync()
        {
            var hasSession = await _sessionService.LoadAsync().ConfigureAwait(false);
            var welcomeText = hasSession && _sessionService.CurrentUser != null
                ? $"Bonjour {_sessionService.CurrentUser.Prenom ?? _sessionService.CurrentUser.Nom ?? _sessionService.CurrentUser.UserIdentifier}!"
                : "Bonjour!";

            await MainThread.InvokeOnMainThreadAsync(() => WelcomeLabel.Text = welcomeText);
        }

        private void LoadCategories()
        {
            Categories.Clear();

            var items = new List<CategoryCard>
            {
                new("Produits", "Gérez le catalogue, les fiches et les stocks."),
                new("Commandes", "Suivi, validation et historique des commandes."),
                new("Clients", "Informations, comptes et fidélité des acheteurs."),
                new("Promotions", "Codes promo, remises et mises en avant."),
                new("Livraisons", "Transporteurs, zones et suivi logistique."),
                new("Paiements", "Modes de paiement et transactions sécurisées."),
                new("Rapports", "Tableaux de bord et indicateurs clés."),
                new("Paramètres", "Configuration générale du site Dantec Market."),
            };

            foreach (var item in items)
                Categories.Add(item);
        }
    }

    public record CategoryCard(string Title, string Description);
}
