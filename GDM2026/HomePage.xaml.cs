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
                new("Dashboard", "Vue d'ensemble et indicateurs clés du site."),
                new("Actualité", "Gérez les actualités et mises à jour du site."),
                new("Catégories", "Organisez les catégories principales du catalogue."),
                new("Super catégories", "Structurez les niveaux supérieurs de navigation."),
                new("Catégories Événements", "Classez les événements et leurs thématiques."),
                new("Commandes", "Suivi, validation et historique des commandes."),
                new("Images", "Bibliothèque média et gestion des visuels."),
                new("Messages", "Centralisez et répondez aux messages reçus."),
                new("Partenaires", "Ajoutez et mettez en avant vos partenaires."),
                new("Réservations", "Gérez les réservations et confirmations associées."),
                new("Produits", "Gérez le catalogue, les fiches et les stocks."),
                new("Utilisateurs", "Droits, profils et accès administrateurs/clients."),
                new("Commentaires", "Modérez les avis et retours des utilisateurs."),
                new("Promo", "Codes promotionnels et campagnes de remise."),
                new("Planning", "Planification des événements et disponibilités."),
                new("Histoire", "Éditez la présentation et l'histoire de l'entreprise."),
                new("Catalogue", "Vue d'ensemble du catalogue et navigation globale."),
            };

            foreach (var item in items)
                Categories.Add(item);
        }
    }

    public record CategoryCard(string Title, string Description);
}
