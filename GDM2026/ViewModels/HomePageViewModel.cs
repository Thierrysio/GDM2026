using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Windows.Input;
using GDM2026.Views;
using System.Diagnostics;

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
        OrderStatusSelectedCommand = new Command<OrderStatusDisplay>(async status => await NavigateToReservationsWithStatusAsync(status));

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
        var isAuthenticated = await LoadSessionAsync();

        Debug.WriteLine($"[HOME] Session loaded: {isAuthenticated}");
        Debug.WriteLine($"[HOME] Token present: {!string.IsNullOrWhiteSpace(_sessionService.AuthToken)}");
        Debug.WriteLine($"[HOME] Token value: {(_sessionService.AuthToken?.Length > 20 ? _sessionService.AuthToken.Substring(0, 20) + "..." : _sessionService.AuthToken)}");

        if (!isAuthenticated || string.IsNullOrWhiteSpace(_sessionService.AuthToken))
        {
            Debug.WriteLine("[HOME] Not authenticated, skipping LoadOrderStatusesAsync");
            return;
        }

        // S'assurer que le token est bien défini dans le factory ET dans l'instance Apis
        AppHttpClientFactory.SetBearerToken(_sessionService.AuthToken);
        _apis.SetBearerToken(_sessionService.AuthToken);
        
        Debug.WriteLine("[HOME] Token set, calling LoadOrderStatusesAsync");
        await LoadOrderStatusesAsync();
    }

    private async Task<bool> LoadSessionAsync()
    {
        var hasSession = await _sessionService.LoadAsync().ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            WelcomeText = hasSession && _sessionService.CurrentUser != null
                ? $"Bonjour {_sessionService.CurrentUser.Prenom ?? _sessionService.CurrentUser.Nom ?? _sessionService.CurrentUser.UserIdentifier}!"
                : "Bonjour!";
        });

        return hasSession && _sessionService.IsAuthenticated;
    }

    private void LoadCategories()
    {
        Categories.Clear();

        var items = new List<CategoryCard>
        {
            new("Dashboard", "Vue d'ensemble et indicateurs clés."),
            new("Actualite", "Dernières nouvelles et publications."),
            new("Categories", "Gestion des catégories principales."),
            new("Catégories Evenements", "Sections dédiées aux événements."),
            new("Images", "Bibliothèque et gestion des médias."),
            new("Messages", "Communication et notifications utilisateurs."),
            new("Partenaires", "Gestion des partenaires et fournisseurs."),
            new("Reservations", "Planning et suivi des réservations."),
            new("Produits", "Gérez le catalogue, les fiches et les stocks."),
            new("Commentaires", "Modération et suivi des avis."),
            new("Promo", "Codes promo, remises et campagnes."),
            new("Planning", "Calendrier et organisation des activités."),
            new("Histoire", "Présentation et historique de Dantec Market."),
            new("Catalogue", "Consultation globale des offres."),
            new("Utiliser Points", "Utiliser les points fidélité d'un client."),
            new("Ajouter Points", "Ajouter des points de fidélité à un client."),
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
            Debug.WriteLine("[HOME] Calling API getNombreCommandes...");
            
            var statuses = await _apis
                .GetAsync<Dictionary<string, int>>("https://dantecmarket.com/api/mobile/getNombreCommandes")
                .ConfigureAwait(false) ?? new Dictionary<string, int>();

            Debug.WriteLine($"[HOME] API returned {statuses.Count} statuses");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                OrderStatuses.Clear();

                foreach (var status in statuses)
                {
                    OrderStatuses.Add(new OrderStatusDisplay(status.Key, status.Value));
                }
            });
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[HOME] HTTP Error: {ex.StatusCode} - {ex.Message}");
            
            // Si 401, le token est invalide - on pourrait rediriger vers login
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Debug.WriteLine("[HOME] Token invalid (401), should redirect to login");
            }
        }
        catch (TaskCanceledException ex)
        {
            Debug.WriteLine($"[HOME] Timeout: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HOME] Unexpected error: {ex.GetType().Name} - {ex.Message}");
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

        if (string.Equals(card.Title, "Actualite", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(ActualitePage), animate: false);
        }

        if (string.Equals(card.Title, "Catégories Evenements", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Title, "Catégories événements", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(EvenementPage), animate: false);
        }

        if (string.Equals(card.Title, "Messages", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(MessagesPage), animate: false);
        }

        if (string.Equals(card.Title, "Partenaires", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(PartnersPage), animate: false);
        }

        if (string.Equals(card.Title, "Reservations", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(ReservationsPage), animate: false);
        }

        if (string.Equals(card.Title, "Produits", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(ProductsPage), animate: false);
        }

        if (string.Equals(card.Title, "Catalogue", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(CataloguePage), animate: false);
        }

        if (string.Equals(card.Title, "Commentaires", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(CommentsPage), animate: false);
        }

        if (string.Equals(card.Title, "Promo", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(PromoPage), animate: false);
        }

        if (string.Equals(card.Title, "Planning", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(PlanningPage), animate: false);
        }

        if (string.Equals(card.Title, "Histoire", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(HistoirePage), animate: false);
        }

        if (string.Equals(card.Title, "Utiliser Points", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(UtiliserPointsFidelitePage), animate: false);
        }

        if (string.Equals(card.Title, "Ajouter Points", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(AjouterPointsFidelitePage), animate: false);
        }

        return Shell.Current.GoToAsync(nameof(CategoryDetailPage), animate: false, new Dictionary<string, object>
        {
            { "card", card }
        });
    }

    private Task NavigateToReservationsWithStatusAsync(OrderStatusDisplay? status)
    {
        if (status == null || Shell.Current == null)
        {
            return Task.CompletedTask;
        }

        // Naviguer vers ReservationsPage avec le statut pré-sélectionné
        return Shell.Current.GoToAsync(nameof(ReservationsPage), animate: false, new Dictionary<string, object>
        {
            { "status", status.Status }
        });
    }
}
