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
using Newtonsoft.Json;
using System.Text;

namespace GDM2026.ViewModels;

public partial class HomePageViewModel : BaseViewModel
{
    private const string AdminCleanupTileName = "Réservé aux administrateurs";
    private const string AdminCleanupPassword = "mayday";
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();
    private string _welcomeText = "Bonjour!";
    private CancellationTokenSource? _refreshCts;

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

    public void StartAutoRefresh()
    {
        StopAutoRefresh();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(20), token);
                    if (!token.IsCancellationRequested)
                    {
                        Debug.WriteLine("[HOME] Auto-refresh: reloading order statuses");
                        await LoadOrderStatusesAsync();
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, token);
    }

    public void StopAutoRefresh()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
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
            new("Vote Collectif", "Votez pour les produits que vous souhaitez en rayon."),
            new("Admin Vote", "Gerez les sessions de vote et les produits candidats."),
            new(AdminCleanupTileName, "Nettoyage des comptes (accès protégé par mot de passe)."),
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

        if (string.Equals(card.Title, "Vote Collectif", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(VoteCollectifPage), animate: false);
        }

        if (string.Equals(card.Title, "Admin Vote", StringComparison.OrdinalIgnoreCase))
        {
            return Shell.Current.GoToAsync(nameof(AdminVotePage), animate: false);
        }

        if (string.Equals(card.Title, AdminCleanupTileName, StringComparison.OrdinalIgnoreCase))
        {
            return ExecuteAdminCleanupAsync();
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

    private async Task ExecuteAdminCleanupAsync()
    {
        if (Shell.Current == null || IsBusy)
        {
            return;
        }

        var password = await Shell.Current.DisplayPromptAsync(
            "Accès administrateur",
            "Saisissez le mot de passe pour lancer le nettoyage des comptes.",
            "Valider",
            "Annuler",
            placeholder: "Mot de passe",
            maxLength: 40,
            keyboard: Keyboard.Text);

        if (password == null)
        {
            return;
        }

        if (!string.Equals(password.Trim(), AdminCleanupPassword, StringComparison.Ordinal))
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.DisplayAlert("Accès refusé", "Mot de passe incorrect.", "OK"));
            return;
        }

        var confirm = await MainThread.InvokeOnMainThreadAsync(async () =>
            await Shell.Current.DisplayAlert(
                "Confirmation",
                "Cette action va supprimer tous les comptes non whitelistés. Continuer ?",
                "Oui",
                "Non"));

        if (!confirm)
        {
            return;
        }

        IsBusy = true;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://dantecmarket.com/api/mobile/delete-all-accounts-except-whitelist")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("User-Agent", "android mobile");

            using var response = await _apis.HttpClient.SendAsync(request).ConfigureAwait(false);
            var rawBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var apiResponse = JsonConvert.DeserializeObject<DeleteAccountsResponse>(rawBody) ?? new DeleteAccountsResponse();

            var message = BuildCleanupMessage(apiResponse, response.StatusCode, rawBody);
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.DisplayAlert("Nettoyage comptes", message, "OK"));
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Shell.Current.DisplayAlert("Nettoyage comptes", $"Erreur inattendue : {ex.Message}", "OK"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildCleanupMessage(DeleteAccountsResponse response, System.Net.HttpStatusCode statusCode, string rawBody)
    {
        if (statusCode == System.Net.HttpStatusCode.OK && response.Success)
        {
            var whitelist = response.WhitelistEmails?.Count > 0
                ? string.Join("\n- ", response.WhitelistEmails)
                : "(aucun email whitelisté fourni)";

            return $"{response.Message}\n\nConservés : {response.KeptUsers}\nSupprimés : {response.DeletedUsers}\n\nWhitelist :\n- {whitelist}";
        }

        if (statusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return response.Message ?? "Route accessible uniquement depuis mobile.";
        }

        if (statusCode == System.Net.HttpStatusCode.InternalServerError)
        {
            return response.Message ?? "Erreur lors du nettoyage côté serveur.";
        }

        return response.Message ?? $"Réponse inattendue ({(int)statusCode}) : {rawBody}";
    }

    private sealed class DeleteAccountsResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("keptUsers")]
        public int KeptUsers { get; set; }

        [JsonProperty("deletedUsers")]
        public int DeletedUsers { get; set; }

        [JsonProperty("whitelistEmails")]
        public List<string>? WhitelistEmails { get; set; }
    }
}
