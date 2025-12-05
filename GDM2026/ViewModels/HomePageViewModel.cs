using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Windows.Input;
using GDM2026;

namespace GDM2026.ViewModels;

public partial class HomePageViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();
    private string _welcomeText = "Bonjour!";

    public HomePageViewModel()
    {
        Categories = [];
        OrderStatuses = [];

        CategorySelectedCommand = new Command<CategoryCard>(async card => await NavigateToCategoryAsync(card));
        OrderStatusSelectedCommand = new Command<OrderStatusDisplay>(async status => await NavigateToOrderStatusAsync(status));

        LoadCategories();
    }

    public ObservableCollection<CategoryCard> Categories { get; }

    public ObservableCollection<OrderStatusDisplay> OrderStatuses { get; }

    public ICommand CategorySelectedCommand { get; }

    public ICommand OrderStatusSelectedCommand { get; }

    public string WelcomeText
    {
        get => _welcomeText;
        set => SetProperty(ref _welcomeText, value);
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(LoadSessionAsync(), LoadOrderStatusesAsync());
    }

    public static void OnDisappearing()
    {
        OrderStatusDeltaTracker.Clear();
    }

    private async Task LoadSessionAsync()
    {
        var hasSession = await _sessionService.LoadAsync().ConfigureAwait(false);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            WelcomeText = hasSession && _sessionService.CurrentUser != null
                ? $"Bonjour {_sessionService.CurrentUser.Prenom ?? _sessionService.CurrentUser.Nom ?? _sessionService.CurrentUser.UserIdentifier}!"
                : "Bonjour!";
        });
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
        {
            Categories.Add(item);
        }
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
                    OrderStatuses.Add(new OrderStatusDisplay(status.Key, status.Value, delta));
                }

                foreach (var delta in deltas.Where(d => !statuses.ContainsKey(d.Key)))
                {
                    OrderStatuses.Add(new OrderStatusDisplay(delta.Key, 0, delta.Value));
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

    private Task NavigateToCategoryAsync(CategoryCard? card)
    {
        if (card == null || Shell.Current == null)
        {
            return Task.CompletedTask;
        }

        if (string.Equals(card.Title, "Images", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(ImageUploadPage), animate: false);
        }

        return Shell.Current.GoToAsync(nameof(CategoryDetailPage), animate: false, new Dictionary<string, object>
        {
            { "card", card }
        });
    }

    private Task NavigateToOrderStatusAsync(OrderStatusDisplay? status)
    {
        if (status == null || Shell.Current == null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync(nameof(OrderStatusPage), animate: false, new Dictionary<string, object>
        {
            { "status", status.Status }
        });
    }
}
