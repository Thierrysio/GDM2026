using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class VoteCollectifViewModel : BaseViewMode
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();
    private bool _sessionLoaded;

    private SessionVote? _currentSession;
    private bool _hasActiveSession;
    private bool _isLoadingSession;
    private bool _isLoadingProduits;
    private bool _isLoadingResultats;
    private bool _isVoting;
    private string _statusMessage = "Chargement des sessions de vote...";
    private Color _statusColor = Colors.Gold;
    private bool _showResultats;
    private ProduitCandidat? _selectedProduit;

    public ObservableCollection<SessionVote> Sessions { get; } = new();
    public ObservableCollection<ProduitCandidat> Produits { get; } = new();
    public ObservableCollection<ResultatVote> Resultats { get; } = new();

    public VoteCollectifViewModel()
    {
        RefreshCommand = new Command(async () => await LoadActiveSessionsAsync());
        VoirProduitsCommand = new Command<SessionVote>(async s => await LoadProduitsAsync(s));
        VoterCommand = new Command<ProduitCandidat>(async p => await ShowVoteDialogAsync(p));
        VoirResultatsCommand = new Command(async () => await LoadResultatsAsync());
        RetourListeCommand = new Command(RetourListe);
    }

    public ICommand RefreshCommand { get; }
    public ICommand VoirProduitsCommand { get; }
    public ICommand VoterCommand { get; }
    public ICommand VoirResultatsCommand { get; }
    public ICommand RetourListeCommand { get; }

    public SessionVote? CurrentSession
    {
        get => _currentSession;
        set
        {
            if (SetProperty(ref _currentSession, value))
            {
                OnPropertyChanged(nameof(HasActiveSession));
                OnPropertyChanged(nameof(SessionTitle));
                OnPropertyChanged(nameof(SessionDescription));
                OnPropertyChanged(nameof(SessionPeriode));
                OnPropertyChanged(nameof(SessionStatut));
                OnPropertyChanged(nameof(CanVote));
                OnPropertyChanged(nameof(CanSeeResultats));
            }
        }
    }

    public bool HasActiveSession
    {
        get => _hasActiveSession;
        set => SetProperty(ref _hasActiveSession, value);
    }

    public bool IsLoadingSession
    {
        get => _isLoadingSession;
        set => SetProperty(ref _isLoadingSession, value);
    }

    public bool IsLoadingProduits
    {
        get => _isLoadingProduits;
        set => SetProperty(ref _isLoadingProduits, value);
    }

    public bool IsLoadingResultats
    {
        get => _isLoadingResultats;
        set => SetProperty(ref _isLoadingResultats, value);
    }

    public bool IsVoting
    {
        get => _isVoting;
        set => SetProperty(ref _isVoting, value);
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

    public bool ShowResultats
    {
        get => _showResultats;
        set
        {
            if (SetProperty(ref _showResultats, value))
            {
                OnPropertyChanged(nameof(ShowProduits));
            }
        }
    }

    public bool ShowProduits => !ShowResultats && HasActiveSession;

    public ProduitCandidat? SelectedProduit
    {
        get => _selectedProduit;
        set => SetProperty(ref _selectedProduit, value);
    }

    public string SessionTitle => _currentSession?.Titre ?? "Aucune session";
    public string SessionDescription => _currentSession?.Description ?? "";
    public string SessionPeriode => _currentSession?.PeriodeLabel ?? "";
    public string SessionStatut => _currentSession?.StatutLabel ?? "";
    public bool CanVote => _currentSession?.IsActive == true;
    public bool CanSeeResultats => _currentSession != null;

    public async Task InitializeAsync()
    {
        await EnsureAuthenticationAsync();
        await LoadActiveSessionsAsync();
    }

    private async Task LoadActiveSessionsAsync()
    {
        if (IsLoadingSession)
            return;

        try
        {
            IsLoadingSession = true;
            StatusMessage = "Chargement des sessions de vote...";
            StatusColor = Colors.Gold;

            var sessions = await _apis.GetListAsync<SessionVote>("/api/mobile/sessions-vote/active");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Sessions.Clear();
                foreach (var session in sessions)
                {
                    Sessions.Add(session);
                }
            });

            if (sessions.Count > 0)
            {
                var activeSession = sessions.FirstOrDefault(s => s.IsActive) ?? sessions[0];
                CurrentSession = activeSession;
                HasActiveSession = true;

                StatusMessage = $"{sessions.Count} session(s) de vote trouvee(s).";
                StatusColor = Colors.LightGreen;

                await LoadProduitsAsync(activeSession);
            }
            else
            {
                CurrentSession = null;
                HasActiveSession = false;
                StatusMessage = "Aucune session de vote active pour le moment.";
                StatusColor = Colors.Gold;
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Debug.WriteLine($"[VOTE] 401 Unauthorized: {ex.Message}");
            StatusMessage = "Authentification requise. Reconnectez-vous.";
            StatusColor = Colors.OrangeRed;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[VOTE] HTTP error: {ex}");
            StatusMessage = "Impossible de charger les sessions de vote.";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VOTE] Error: {ex}");
            StatusMessage = $"Erreur : {ex.Message}";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsLoadingSession = false;
        }
    }

    private async Task LoadProduitsAsync(SessionVote? session)
    {
        if (session == null || IsLoadingProduits)
            return;

        try
        {
            IsLoadingProduits = true;
            CurrentSession = session;
            HasActiveSession = true;
            ShowResultats = false;

            var userId = _sessionService.CurrentUser?.Id ?? 0;

            var produits = await _apis.PostAsync<object, List<ProduitCandidat>>(
                "/api/mobile/sessions-vote/produits",
                new { sessionVoteId = session.Id, userId });

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Produits.Clear();
                if (produits != null)
                {
                    foreach (var produit in produits)
                    {
                        Produits.Add(produit);
                    }
                }
            });

            StatusMessage = produits?.Count > 0
                ? $"{produits.Count} produit(s) candidat(s) a voter."
                : "Aucun produit candidat dans cette session.";
            StatusColor = Colors.LightGreen;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[VOTE] HTTP error loading produits: {ex}");
            StatusMessage = "Impossible de charger les produits candidats.";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VOTE] Error loading produits: {ex}");
            StatusMessage = $"Erreur : {ex.Message}";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsLoadingProduits = false;
        }
    }

    private async Task ShowVoteDialogAsync(ProduitCandidat? produit)
    {
        if (produit == null || _currentSession == null || IsVoting)
            return;

        if (!CanVote)
        {
            StatusMessage = "Cette session de vote n'est plus active.";
            StatusColor = Colors.OrangeRed;
            return;
        }

        var shell = Shell.Current;
        if (shell == null)
            return;

        var noteStr = await shell.DisplayPromptAsync(
            produit.DisplayName,
            produit.HasUserVote
                ? $"Modifier votre note (actuellement {produit.NoteUtilisateur:F0}/5) :"
                : "Donnez une note de 1 a 5 :",
            accept: "Voter",
            cancel: "Annuler",
            placeholder: "1 a 5",
            maxLength: 1,
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(noteStr))
            return;

        if (!double.TryParse(noteStr, out var note) || note < 1 || note > 5)
        {
            StatusMessage = "La note doit etre entre 1 et 5.";
            StatusColor = Colors.OrangeRed;
            return;
        }

        await SubmitVoteAsync(produit, note);
    }

    private async Task SubmitVoteAsync(ProduitCandidat produit, double note)
    {
        if (_currentSession == null)
            return;

        try
        {
            IsVoting = true;
            StatusMessage = "Envoi du vote...";
            StatusColor = Colors.Gold;

            var request = new VoteRequest
            {
                UserId = _sessionService.CurrentUser?.Id ?? 0,
                ProduitCandidatId = produit.Id,
                SessionVoteId = _currentSession.Id,
                Note = note
            };

            var response = await _apis.PostAsync<VoteRequest, VoteResponse>(
                "/api/mobile/sessions-vote/voter", request);

            if (response?.Success == true)
            {
                StatusMessage = response.Message ?? "Vote enregistre avec succes.";
                StatusColor = Colors.LightGreen;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    produit.NoteUtilisateur = note;
                    produit.MoyenneNotes = response.DisplayMoyenne;
                    produit.NoteMoyenne = response.DisplayMoyenne;

                    var newNombreVotes = response.DisplayNombreVotes;
                    if (newNombreVotes > 0)
                    {
                        var index = Produits.IndexOf(produit);
                        if (index >= 0)
                        {
                            var updated = Produits[index];
                            Produits.RemoveAt(index);
                            updated.NombreVotes = newNombreVotes;
                            updated.NoteUtilisateur = note;
                            updated.MoyenneNotes = response.DisplayMoyenne;
                            updated.NoteMoyenne = response.DisplayMoyenne;
                            Produits.Insert(index, updated);
                        }
                    }
                });
            }
            else
            {
                StatusMessage = response?.Message ?? "Erreur lors du vote.";
                StatusColor = Colors.OrangeRed;
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[VOTE] HTTP error voting: {ex}");
            StatusMessage = "Impossible d'envoyer le vote.";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VOTE] Error voting: {ex}");
            StatusMessage = $"Erreur : {ex.Message}";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsVoting = false;
        }
    }

    private async Task LoadResultatsAsync()
    {
        if (_currentSession == null || IsLoadingResultats)
            return;

        try
        {
            IsLoadingResultats = true;
            StatusMessage = "Chargement des resultats...";
            StatusColor = Colors.Gold;

            var resultats = await _apis.PostAsync<object, List<ResultatVote>>(
                "/api/mobile/sessions-vote/resultats",
                new { sessionVoteId = _currentSession.Id }) ?? new List<ResultatVote>();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Resultats.Clear();
                foreach (var resultat in resultats.OrderBy(r => r.Classement))
                {
                    Resultats.Add(resultat);
                }

                ShowResultats = true;
            });

            StatusMessage = resultats.Count > 0
                ? "Classement des produits candidats."
                : "Aucun resultat disponible.";
            StatusColor = Colors.LightGreen;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[VOTE] HTTP error loading resultats: {ex}");
            StatusMessage = "Impossible de charger les resultats.";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VOTE] Error loading resultats: {ex}");
            StatusMessage = $"Erreur : {ex.Message}";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsLoadingResultats = false;
        }
    }

    private void RetourListe()
    {
        ShowResultats = false;
        OnPropertyChanged(nameof(ShowProduits));
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

        StatusMessage = "Connexion requise pour voter.";
        StatusColor = Colors.OrangeRed;
        return false;
    }
}
