using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

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
                new("Dashboard", "Vue d'ensemble et indicateurs clés."),
                new("Actualite", "Dernières nouvelles et publications."),
                new("Categories", "Gestion des catégories principales."),
                new("Super categories", "Organisation des catégories parentes."),
                new("Catégories Evenements", "Sections dédiées aux événements."),
                new("Commandes", "Suivi, validation et historique des commandes."),
                new("Images", "Bibliothèque et gestion des médias."),
                new("Messages", "Communication et notifications utilisateurs."),
                new("Partenaires", "Gestion des partenaires et fournisseurs."),
                new("Reservations", "Planning et suivi des réservations."),
                new("Produits", "Gérez le catalogue, les fiches et les stocks."),
                new("Utilisateurs", "Comptes, rôles et informations des membres."),
                new("Commentaires", "Modération et suivi des avis."),
                new("Promo", "Codes promo, remises et campagnes."),
                new("Planning", "Calendrier et organisation des activités."),
                new("Histoire", "Présentation et historique de Dantec Market."),
                new("Catalogue", "Consultation globale des offres."),
            };

            foreach (var item in items)
                Categories.Add(item);
        }

        private async void OnCategorySelected(object sender, SelectionChangedEventArgs e)
        {
            var selectedCard = e.CurrentSelection?.FirstOrDefault() as CategoryCard;

            if (selectedCard == null)
            {
                return;
            }

            if (sender is CollectionView collectionView)
            {
                collectionView.SelectedItem = null;
            }

            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync(nameof(CategoryDetailPage), new Dictionary<string, object>
                {
                    { "card", selectedCard }
                });
            }
        }
    }

    public record CategoryCard(string Title, string Description);
}
