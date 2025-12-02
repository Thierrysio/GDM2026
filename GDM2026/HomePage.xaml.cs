using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using System;
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
        private const string StatusChangedMessage = "OrderStatusChanged";
        private readonly Apis _apis = new();
        private readonly SessionService _sessionService = new();
        private readonly Dictionary<string, int> _statusDeltas = new(StringComparer.Ordinal);

        public ObservableCollection<CategoryCard> Categories { get; } = new();
        public ObservableCollection<OrderStatus> OrderStatuses { get; } = new();

        public HomePage()
        {
            InitializeComponent();
            BindingContext = this;
            LoadCategories();

            MessagingCenter.Subscribe<OrderStatusPage, OrderStatusChange>(this, StatusChangedMessage, OnOrderStatusChanged);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await Task.WhenAll(LoadSessionAsync(), LoadOrderStatusesAsync()).ConfigureAwait(false);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ClearStatusAdjustments();
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
                    .ConfigureAwait(false);

                if (statuses == null)
                {
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    OrderStatuses.Clear();

                    foreach (var status in statuses)
                    {
                        OrderStatuses.Add(new OrderStatus(status.Key, status.Value));
                    }

                    foreach (var delta in _statusDeltas)
                    {
                        ApplyStatusDelta(delta.Key, delta.Value, updateTracker: false);
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

        private void OnOrderStatusChanged(OrderStatusPage sender, OrderStatusChange change)
        {
            if (change is null)
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ApplyStatusDelta(change.PreviousStatus, -1);
                ApplyStatusDelta(change.NewStatus, 1);
            });
        }

        private void ApplyStatusDelta(string? status, int delta, bool updateTracker = true)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            var existing = OrderStatuses.FirstOrDefault(s => string.Equals(s.Status, status, StringComparison.Ordinal));

            if (existing == null)
            {
                existing = new OrderStatus(status, 0);
                OrderStatuses.Add(existing);
            }

            existing.ApplyDelta(delta);

            if (!updateTracker)
            {
                return;
            }

            _statusDeltas.TryGetValue(status, out var currentDelta);
            var newDelta = currentDelta + delta;

            if (newDelta == 0)
            {
                _statusDeltas.Remove(status);
            }
            else
            {
                _statusDeltas[status] = newDelta;
            }
        }

        private void ClearStatusAdjustments()
        {
            foreach (var status in OrderStatuses)
            {
                status.ResetDelta();
            }

            _statusDeltas.Clear();
        }
    }

    public record CategoryCard(string Title, string Description);

    public class OrderStatus : INotifyPropertyChanged
    {
        private int _count;
        private int _delta;

        public OrderStatus(string status, int count)
        {
            Status = status;
            _count = count;
        }

        public string Status { get; }

        public int Count
        {
            get => _count;
            set
            {
                if (_count != value)
                {
                    _count = value;
                    OnPropertyChanged(nameof(Count));
                    OnPropertyChanged(nameof(DisplayCount));
                }
            }
        }

        public int Delta
        {
            get => _delta;
            private set
            {
                if (_delta != value)
                {
                    _delta = value;
                    OnPropertyChanged(nameof(Delta));
                    OnPropertyChanged(nameof(DisplayCount));
                }
            }
        }

        public string DisplayCount =>
            Delta == 0
                ? Count.ToString()
                : $"{Count} ({(Delta > 0 ? "+" : string.Empty)}{Delta})";

        public event PropertyChangedEventHandler? PropertyChanged;

        public void ApplyDelta(int delta) => Delta += delta;

        public void ResetDelta() => Delta = 0;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public record OrderStatusChange(string PreviousStatus, string NewStatus);
}
