using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class AdminVoteViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();
    private bool _sessionLoaded;

    // --- Session creation fields ---
    private string _newSessionTitre = string.Empty;
    private string _newSessionDescription = string.Empty;
    private DateTime _newSessionDateDebut = DateTime.Today;
    private DateTime _newSessionDateFin = DateTime.Today.AddDays(14);

    // --- Product creation fields ---
    private string _newProduitNom = string.Empty;
    private string _newProduitDescription = string.Empty;
    private string _newProduitPrix = string.Empty;
    private string _newProduitImageUrl = string.Empty;
    private SubCategory? _selectedCategorie;

    // --- State ---
    private bool _isLoadingSessions;
    private bool _isCreatingSession;
    private bool _isCreatingProduit;
    private bool _isDeletingProduit;
    private SessionVote? _selectedSession;
    private string _statusMessage = "Chargement...";
    private Color _statusColor = Colors.Gold;
    private bool _showSessionForm;
    private bool _showProduitForm;
    private bool _showSessionDetail;
    private bool _isLoadingImages;
    private AdminImage? _selectedLibraryImage;
    private string _imageSearchTerm = string.Empty;

    public ObservableCollection<SessionVote> Sessions { get; } = new();
    public ObservableCollection<ProduitCandidat> Produits { get; } = new();
    public ObservableCollection<SubCategory> AvailableCategories { get; } = new();
    public ObservableCollection<AdminImage> ImageLibrary { get; } = new();
    public ObservableCollection<AdminImage> FilteredImageLibrary { get; } = new();

    public AdminVoteViewModel()
    {
        RefreshCommand = new Command(async () => await LoadSessionsAsync());
        ShowSessionFormCommand = new Command(() => ShowSessionForm = !ShowSessionForm);
        CreateSessionCommand = new Command(async () => await CreateSessionAsync());
        SelectSessionCommand = new Command<SessionVote>(async s => await SelectSessionAsync(s));
        ShowProduitFormCommand = new Command(() => ShowProduitForm = !ShowProduitForm);
        CreateProduitCommand = new Command(async () => await CreateProduitAsync());
        DeleteProduitCommand = new Command<ProduitCandidat>(async p => await DeleteProduitAsync(p));
        RetourListeCommand = new Command(RetourListe);
        ActivateSessionCommand = new Command(async () => await ChangeSessionStatusAsync("active"));
        CloseSessionCommand = new Command(async () => await ChangeSessionStatusAsync("terminee"));
    }

    // --- Commands ---
    public ICommand RefreshCommand { get; }
    public ICommand ShowSessionFormCommand { get; }
    public ICommand CreateSessionCommand { get; }
    public ICommand SelectSessionCommand { get; }
    public ICommand ShowProduitFormCommand { get; }
    public ICommand CreateProduitCommand { get; }
    public ICommand DeleteProduitCommand { get; }
    public ICommand RetourListeCommand { get; }
    public ICommand ActivateSessionCommand { get; }
    public ICommand CloseSessionCommand { get; }

    // --- Session creation properties ---
    public string NewSessionTitre
    {
        get => _newSessionTitre;
        set => SetProperty(ref _newSessionTitre, value);
    }

    public string NewSessionDescription
    {
        get => _newSessionDescription;
        set => SetProperty(ref _newSessionDescription, value);
    }

    public DateTime NewSessionDateDebut
    {
        get => _newSessionDateDebut;
        set => SetProperty(ref _newSessionDateDebut, value);
    }

    public DateTime NewSessionDateFin
    {
        get => _newSessionDateFin;
        set => SetProperty(ref _newSessionDateFin, value);
    }

    // --- Product creation properties ---
    public string NewProduitNom
    {
        get => _newProduitNom;
        set => SetProperty(ref _newProduitNom, value);
    }

    public string NewProduitDescription
    {
        get => _newProduitDescription;
        set => SetProperty(ref _newProduitDescription, value);
    }

    public string NewProduitPrix
    {
        get => _newProduitPrix;
        set => SetProperty(ref _newProduitPrix, value);
    }

    public string NewProduitImageUrl
    {
        get => _newProduitImageUrl;
        set => SetProperty(ref _newProduitImageUrl, value);
    }

    public SubCategory? SelectedCategorie
    {
        get => _selectedCategorie;
        set => SetProperty(ref _selectedCategorie, value);
    }

    public AdminImage? SelectedLibraryImage
    {
        get => _selectedLibraryImage;
        set
        {
            if (SetProperty(ref _selectedLibraryImage, value) && value != null)
            {
                NewProduitImageUrl = value.Url;
            }
        }
    }

    public string ImageSearchTerm
    {
        get => _imageSearchTerm;
        set
        {
            if (SetProperty(ref _imageSearchTerm, value))
            {
                RefreshImageFilter();
            }
        }
    }

    // --- State properties ---
    public bool IsLoadingSessions
    {
        get => _isLoadingSessions;
        set => SetProperty(ref _isLoadingSessions, value);
    }

    public bool IsCreatingSession
    {
        get => _isCreatingSession;
        set => SetProperty(ref _isCreatingSession, value);
    }

    public bool IsCreatingProduit
    {
        get => _isCreatingProduit;
        set => SetProperty(ref _isCreatingProduit, value);
    }

    public bool IsDeletingProduit
    {
        get => _isDeletingProduit;
        set => SetProperty(ref _isDeletingProduit, value);
    }

    public bool IsLoadingImages
    {
        get => _isLoadingImages;
        set => SetProperty(ref _isLoadingImages, value);
    }

    public SessionVote? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                OnPropertyChanged(nameof(SessionTitle));
                OnPropertyChanged(nameof(SessionDescription));
                OnPropertyChanged(nameof(SessionPeriode));
                OnPropertyChanged(nameof(SessionStatut));
                OnPropertyChanged(nameof(CanActivate));
                OnPropertyChanged(nameof(CanClose));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Color StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    public bool ShowSessionForm
    {
        get => _showSessionForm;
        set => SetProperty(ref _showSessionForm, value);
    }

    public bool ShowProduitForm
    {
        get => _showProduitForm;
        set => SetProperty(ref _showProduitForm, value);
    }

    public bool ShowSessionDetail
    {
        get => _showSessionDetail;
        set
        {
            if (SetProperty(ref _showSessionDetail, value))
            {
                OnPropertyChanged(nameof(ShowSessionList));
            }
        }
    }

    public bool ShowSessionList => !ShowSessionDetail;

    public string SessionTitle => _selectedSession?.Titre ?? "";
    public string SessionDescription => _selectedSession?.Description ?? "";
    public string SessionPeriode => _selectedSession?.PeriodeLabel ?? "";
    public string SessionStatut => _selectedSession?.StatutLabel ?? "";
    public bool CanActivate => _selectedSession != null && !_selectedSession.IsActive;
    public bool CanClose => _selectedSession?.IsActive == true;

    public async Task InitializeAsync()
    {
        await EnsureAuthenticationAsync();
        await LoadSessionsAsync();
        await LoadCategoriesAsync();
        await LoadImageLibraryAsync();
    }

    private async Task LoadSessionsAsync()
    {
        if (IsLoadingSessions) return;

        try
        {
            IsLoadingSessions = true;
            StatusMessage = "Chargement des sessions...";
            StatusColor = Colors.Gold;

            var sessions = await _apis.GetListAsync<SessionVote>("/api/mobile/sessions-vote/all");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Sessions.Clear();
                foreach (var session in sessions.OrderByDescending(s => s.DateDebut))
                {
                    Sessions.Add(session);
                }
            });

            StatusMessage = sessions.Count > 0
                ? $"{sessions.Count} session(s) de vote."
                : "Aucune session de vote. Creez-en une !";
            StatusColor = Colors.LightGreen;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            StatusMessage = "Authentification requise.";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ADMIN_VOTE] Error loading sessions: {ex}");
            StatusMessage = "Impossible de charger les sessions.";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsLoadingSessions = false;
        }
    }

    private async Task CreateSessionAsync()
    {
        if (IsCreatingSession) return;

        if (string.IsNullOrWhiteSpace(NewSessionTitre))
        {
            StatusMessage = "Le titre de la session est obligatoire.";
            StatusColor = Colors.OrangeRed;
            return;
        }

        if (NewSessionDateFin <= NewSessionDateDebut)
        {
            StatusMessage = "La date de fin doit etre apres la date de debut.";
            StatusColor = Colors.OrangeRed;
            return;
        }

        try
        {
            IsCreatingSession = true;
            StatusMessage = "Creation de la session...";
            StatusColor = Colors.Gold;

            var request = new CreateSessionVoteRequest
            {
                Titre = NewSessionTitre.Trim(),
                Description = NewSessionDescription.Trim(),
                DateDebut = NewSessionDateDebut.ToString("yyyy-MM-dd"),
                DateFin = NewSessionDateFin.ToString("yyyy-MM-dd")
            };

            var response = await _apis.PostAsync<CreateSessionVoteRequest, SessionVoteResponse>(
                "/api/mobile/sessions-vote/create", request);

            if (response?.Success == true)
            {
                StatusMessage = response.Message ?? "Session creee avec succes.";
                StatusColor = Colors.LightGreen;

                NewSessionTitre = string.Empty;
                NewSessionDescription = string.Empty;
                NewSessionDateDebut = DateTime.Today;
                NewSessionDateFin = DateTime.Today.AddDays(14);
                ShowSessionForm = false;

                await LoadSessionsAsync();
            }
            else
            {
                StatusMessage = response?.Message ?? "Erreur lors de la creation.";
                StatusColor = Colors.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ADMIN_VOTE] Error creating session: {ex}");
            StatusMessage = "Impossible de creer la session.";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsCreatingSession = false;
        }
    }

    private async Task SelectSessionAsync(SessionVote? session)
    {
        if (session == null) return;

        SelectedSession = session;
        ShowSessionDetail = true;
        ShowProduitForm = false;

        await LoadProduitsForSessionAsync(session.Id);
    }

    private async Task LoadProduitsForSessionAsync(int sessionId)
    {
        try
        {
            StatusMessage = "Chargement des produits candidats...";
            StatusColor = Colors.Gold;

            var produits = await _apis.PostAsync<object, List<ProduitCandidat>>(
                "/api/mobile/sessions-vote/produits-admin",
                new { sessionVoteId = sessionId }) ?? new List<ProduitCandidat>();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Produits.Clear();
                foreach (var produit in produits)
                {
                    Produits.Add(produit);
                }
            });

            StatusMessage = produits.Count > 0
                ? $"{produits.Count} produit(s) candidat(s)."
                : "Aucun produit candidat. Ajoutez-en !";
            StatusColor = Colors.LightGreen;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ADMIN_VOTE] Error loading produits: {ex}");
            StatusMessage = "Impossible de charger les produits.";
            StatusColor = Colors.OrangeRed;
        }
    }

    private async Task CreateProduitAsync()
    {
        if (IsCreatingProduit || _selectedSession == null) return;

        if (string.IsNullOrWhiteSpace(NewProduitNom))
        {
            StatusMessage = "Le nom du produit est obligatoire.";
            StatusColor = Colors.OrangeRed;
            return;
        }

        decimal? prix = null;
        if (!string.IsNullOrWhiteSpace(NewProduitPrix))
        {
            var culture = CultureInfo.GetCultureInfo("fr-FR");
            if (decimal.TryParse(NewProduitPrix, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, culture, out var parsed) && parsed > 0)
            {
                prix = parsed;
            }
            else
            {
                StatusMessage = "Prix invalide. Utilisez le format 14,90.";
                StatusColor = Colors.OrangeRed;
                return;
            }
        }

        try
        {
            IsCreatingProduit = true;
            StatusMessage = "Ajout du produit candidat...";
            StatusColor = Colors.Gold;

            var request = new CreateProduitCandidatRequest
            {
                SessionVoteId = _selectedSession.Id,
                NomProduit = NewProduitNom.Trim(),
                DescriptionCourte = NewProduitDescription.Trim(),
                PrixEstime = prix,
                ImageUrl = string.IsNullOrWhiteSpace(NewProduitImageUrl) ? null : NewProduitImageUrl.Trim(),
                Categorie = _selectedCategorie?.Name
            };

            var response = await _apis.PostAsync<CreateProduitCandidatRequest, SessionVoteResponse>(
                "/api/mobile/sessions-vote/produit-candidat/create", request);

            if (response?.Success == true)
            {
                StatusMessage = response.Message ?? "Produit candidat ajoute.";
                StatusColor = Colors.LightGreen;

                NewProduitNom = string.Empty;
                NewProduitDescription = string.Empty;
                NewProduitPrix = string.Empty;
                NewProduitImageUrl = string.Empty;
                SelectedCategorie = null;
                SelectedLibraryImage = null;
                ShowProduitForm = false;

                await LoadProduitsForSessionAsync(_selectedSession.Id);
            }
            else
            {
                StatusMessage = response?.Message ?? "Erreur lors de l'ajout.";
                StatusColor = Colors.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ADMIN_VOTE] Error creating produit: {ex}");
            StatusMessage = "Impossible d'ajouter le produit.";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsCreatingProduit = false;
        }
    }

    private async Task DeleteProduitAsync(ProduitCandidat? produit)
    {
        if (produit == null || _selectedSession == null || IsDeletingProduit) return;

        if (Shell.Current == null) return;

        var confirm = await MainThread.InvokeOnMainThreadAsync(async () =>
            await Shell.Current.DisplayAlert(
                "Supprimer",
                $"Supprimer le produit candidat \"{produit.DisplayName}\" ?",
                "Oui", "Non"));

        if (!confirm) return;

        try
        {
            IsDeletingProduit = true;
            StatusMessage = "Suppression...";
            StatusColor = Colors.Gold;

            var success = await _apis.PostBoolAsync(
                "/api/mobile/sessions-vote/produit-candidat/delete",
                new { produitCandidatId = produit.Id, sessionVoteId = _selectedSession.Id });

            if (success)
            {
                StatusMessage = "Produit candidat supprime.";
                StatusColor = Colors.LightGreen;
                await LoadProduitsForSessionAsync(_selectedSession.Id);
            }
            else
            {
                StatusMessage = "Echec de la suppression.";
                StatusColor = Colors.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ADMIN_VOTE] Error deleting produit: {ex}");
            StatusMessage = "Impossible de supprimer le produit.";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsDeletingProduit = false;
        }
    }

    private async Task ChangeSessionStatusAsync(string newStatus)
    {
        if (_selectedSession == null) return;

        if (Shell.Current == null) return;

        var label = newStatus == "active" ? "activer" : "cloturer";
        var confirm = await MainThread.InvokeOnMainThreadAsync(async () =>
            await Shell.Current.DisplayAlert(
                "Confirmation",
                $"Voulez-vous {label} la session \"{_selectedSession.Titre}\" ?",
                "Oui", "Non"));

        if (!confirm) return;

        try
        {
            StatusMessage = $"Changement de statut...";
            StatusColor = Colors.Gold;

            var success = await _apis.PostBoolAsync(
                "/api/mobile/sessions-vote/status",
                new { sessionVoteId = _selectedSession.Id, statut = newStatus });

            if (success)
            {
                _selectedSession.Statut = newStatus;
                OnPropertyChanged(nameof(SessionStatut));
                OnPropertyChanged(nameof(CanActivate));
                OnPropertyChanged(nameof(CanClose));

                StatusMessage = $"Session {label}e avec succes.";
                StatusColor = Colors.LightGreen;
            }
            else
            {
                StatusMessage = $"Echec du changement de statut.";
                StatusColor = Colors.OrangeRed;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ADMIN_VOTE] Error changing status: {ex}");
            StatusMessage = "Impossible de changer le statut.";
            StatusColor = Colors.OrangeRed;
        }
    }

    private void RetourListe()
    {
        ShowSessionDetail = false;
        SelectedSession = null;
        Produits.Clear();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await _apis.GetListAsync<SubCategory>("/api/crud/categorie/list");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AvailableCategories.Clear();
                foreach (var cat in categories.OrderBy(c => c.Name))
                {
                    AvailableCategories.Add(cat);
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ADMIN_VOTE] Error loading categories: {ex}");
        }
    }

    private async Task LoadImageLibraryAsync()
    {
        if (IsLoadingImages) return;

        try
        {
            IsLoadingImages = true;

            var images = await _apis.GetListAsync<AdminImage>("/api/crud/images/list");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ImageLibrary.Clear();
                foreach (var img in images)
                {
                    ImageLibrary.Add(img);
                }
                RefreshImageFilter();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ADMIN_VOTE] Error loading images: {ex}");
        }
        finally
        {
            IsLoadingImages = false;
        }
    }

    private void RefreshImageFilter()
    {
        var hasSearch = !string.IsNullOrWhiteSpace(ImageSearchTerm);
        var normalized = ImageSearchTerm?.Trim().ToLowerInvariant();

        IEnumerable<AdminImage> source = ImageLibrary;
        if (hasSearch)
        {
            source = source.Where(img =>
                (!string.IsNullOrWhiteSpace(img.DisplayName) && img.DisplayName.ToLowerInvariant().Contains(normalized!)) ||
                (!string.IsNullOrWhiteSpace(img.Url) && img.Url.ToLowerInvariant().Contains(normalized!)));
        }

        FilteredImageLibrary.Clear();
        foreach (var img in source)
        {
            FilteredImageLibrary.Add(img);
        }
    }

    private async Task<bool> EnsureAuthenticationAsync()
    {
        if (!_sessionLoaded)
        {
            _sessionLoaded = true;
            await _sessionService.LoadAsync();
        }

        if (!string.IsNullOrWhiteSpace(_sessionService.AuthToken))
        {
            _apis.SetBearerToken(_sessionService.AuthToken);
            return true;
        }

        StatusMessage = "Connexion requise.";
        StatusColor = Colors.OrangeRed;
        return false;
    }
}
