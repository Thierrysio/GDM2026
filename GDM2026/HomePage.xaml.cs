using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace GDM2026
{
    public partial class HomePage : ContentPage
    {
        private readonly Apis _apis = new();
        private readonly SessionService _sessionService = new();

        public ObservableCollection<CategoryCard> Categories { get; } = new();
        public ObservableCollection<OrderStatus> OrderStatuses { get; } = new();

        public HomePage()
        {
            InitializeComponent();
            BindingContext = this;
            LoadCategories();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await Task.WhenAll(LoadSessionAsync(), LoadOrderStatusesAsync()).ConfigureAwait(false);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            OrderStatusDeltaTracker.Clear();
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

        private async Task LoadOrderStatusesAsync()
        {
            try
            {
                var statuses = await _apis
                    .GetAsync<Dictionary<string, int>>("https://dantecmarket.com/api/mobile/getNombreCommandes")
                    .ConfigureAwait(false) ?? new Dictionary<string, int>();

                var deltas = OrderStatusDeltaTracker.GetDeltas();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    OrderStatuses.Clear();

                    foreach (var status in statuses)
                    {
                        var delta = deltas.TryGetValue(status.Key, out var change) ? change : 0;
                        OrderStatuses.Add(new OrderStatus(status.Key, status.Value, delta));
                    }

                    foreach (var delta in deltas.Where(d => !statuses.ContainsKey(d.Key)))
                    {
                        OrderStatuses.Add(new OrderStatus(delta.Key, 0, delta.Value));
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // Ignore timeout and keep the existing data.
            }
            catch (HttpRequestException)
            {
                // Ignore network/API errors and keep the existing data.
            }
            catch (Exception)
            {
                // Ignore unexpected errors to avoid breaking the UI lifecycle.
            }
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

            await NavigateToCategoryAsync(selectedCard).ConfigureAwait(false);
        }

        private async void OnCategoryTapped(object sender, TappedEventArgs e)
        {
            if (sender is BindableObject bindable && bindable.BindingContext is CategoryCard card)
            {
                await NavigateToCategoryAsync(card).ConfigureAwait(false);
            }
        }

        private async void OnOrderStatusTapped(object sender, TappedEventArgs e)
        {
            if (sender is BindableObject bindable && bindable.BindingContext is OrderStatus status)
            {
                await NavigateToOrderStatusAsync(status).ConfigureAwait(false);
            }
        }

        private Task NavigateToCategoryAsync(CategoryCard card)
        {
            if (Shell.Current == null)
            {
                return Task.CompletedTask;
            }

            return Shell.Current.GoToAsync(nameof(CategoryDetailPage), new Dictionary<string, object>
            {
                { "card", card }
            });
        }

        private Task NavigateToOrderStatusAsync(OrderStatus status)
        {
            if (Shell.Current == null)
            {
                return Task.CompletedTask;
            }

            return Shell.Current.GoToAsync(nameof(OrderStatusPage), new Dictionary<string, object>
            {
                { "status", status.Status }
            });
        }
    }

    public record CategoryCard(string Title, string Description);

    public class OrderStatus : INotifyPropertyChanged
    {
        private int _count;
        private int _delta;

        public OrderStatus(string status, int count, int delta = 0)
        {
            Status = status;
            _count = count;
            _delta = delta;
        }

        public string Status { get; }

        public int Count
        {
            get => _count;
            private set => SetProperty(ref _count, value);
        }

        public int Delta
        {
            get => _delta;
            private set
            {
                if (SetProperty(ref _delta, value))
                {
                    OnPropertyChanged(nameof(DisplayCount));
                }
            }
        }

        public string DisplayCount => Delta != 0 ? $"{Count} ({(Delta > 0 ? "+" : string.Empty)}{Delta})" : Count.ToString();

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetProperty<T>(ref T backingStore, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
            {
                return false;
            }

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
