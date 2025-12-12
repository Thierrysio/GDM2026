using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class MessagesViewModel : BaseViewModel
{
    private const int PageSize = 10;

    // ✅ Mets ici EXACTEMENT ce que ton backend utilise
    private const string EtatATraiter = "a traiter";
    private const string EtatTraite = "traité";

    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionPrepared;
    private bool _isLoading;
    private bool _isSubmitting;

    private int _pageIndex;
    private bool _hasLoadedOnce;

    private string _feedbackMessage = "Cliquez sur « Charger les 10 derniers » pour afficher les messages à traiter.";
    private Color _feedbackColor = Colors.Gold;

    private MessageEntry? _selectedMessage;
    private string _replyText = string.Empty;

    // Cache local : uniquement “A traiter” triés par date desc.
    private List<MessageEntry> _pendingCache = new();

    public ObservableCollection<MessageEntry> PendingMessages { get; } = new();

    public ICommand LoadLatestCommand { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand SendReplyCommand { get; }

    public MessagesViewModel()
    {
        LoadLatestCommand = new Command(async () => await LoadLatestAsync(), () => !IsLoading && !IsSubmitting);
        LoadMoreCommand = new Command(async () => await LoadMoreAsync(), () => !IsLoading && !IsSubmitting && HasMore);
        SendReplyCommand = new Command(async () => await SendReplyAsync(), CanSendReply);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
                RefreshCommands();
        }
    }

    public bool IsSubmitting
    {
        get => _isSubmitting;
        set
        {
            if (SetProperty(ref _isSubmitting, value))
                RefreshCommands();
        }
    }

    public string FeedbackMessage
    {
        get => _feedbackMessage;
        set => SetProperty(ref _feedbackMessage, value);
    }

    public Color FeedbackColor
    {
        get => _feedbackColor;
        set => SetProperty(ref _feedbackColor, value);
    }

    public MessageEntry? SelectedMessage
    {
        get => _selectedMessage;
        set
        {
            if (SetProperty(ref _selectedMessage, value))
            {
                ReplyText = value?.Reponse ?? string.Empty;
                RefreshCommands();
            }
        }
    }

    public string ReplyText
    {
        get => _replyText;
        set
        {
            if (SetProperty(ref _replyText, value))
                RefreshCommands();
        }
    }

    public bool HasMore => _pendingCache.Count > PendingMessages.Count;

    public string LoadMoreText => HasMore ? "Voir plus (10)" : "Aucun autre message";

    public async Task InitializeAsync()
    {
        if (!_sessionPrepared)
            await PrepareSessionAsync();

        FeedbackMessage = "Cliquez sur « Charger les 10 derniers » pour afficher les messages à traiter.";
        FeedbackColor = Colors.Gold;
        RefreshCommands();
    }

    private async Task PrepareSessionAsync()
    {
        try
        {
            await _sessionService.LoadAsync();
            _apis.SetBearerToken(_sessionService.AuthToken);
            _sessionPrepared = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MESSAGES] Session non préparée : {ex}");
            _sessionPrepared = true;
        }
    }

    private async Task LoadLatestAsync()
    {
        if (IsLoading || IsSubmitting) return;

        try
        {
            IsLoading = true;
            FeedbackMessage = "Chargement des messages à traiter…";
            FeedbackColor = Colors.Gold;

            if (!_sessionPrepared)
                await PrepareSessionAsync();

            var items = await _apis.GetListAsync<MessageEntry>("/api/crud/messages/list");

            _pendingCache = (items ?? new List<MessageEntry>())
                .Where(m => IsEtatATraiter(m.Etat))
                .OrderByDescending(GetMessageDate)
                .ToList();

            _pageIndex = 0;
            PendingMessages.Clear();

            AppendNextPage();

            _hasLoadedOnce = true;

            FeedbackMessage = PendingMessages.Count == 0
                ? "Aucun message à traiter."
                : $"{PendingMessages.Count} message(s) à traiter affiché(s).";
            FeedbackColor = Colors.LightGreen;

            RefreshCommands();
        }
        catch (OperationCanceledException)
        {
            FeedbackMessage = "Chargement annulé.";
            FeedbackColor = Colors.Orange;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            FeedbackMessage = "Accès refusé. Veuillez vous reconnecter.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] 401 lors du chargement : {ex}");
        }
        catch (Exception ex)
        {
            FeedbackMessage = "Impossible de charger les messages.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] Erreur de chargement : {ex}");
        }
        finally
        {
            IsLoading = false;
            RefreshCommands();
        }
    }

    private async Task LoadMoreAsync()
    {
        if (IsLoading || IsSubmitting) return;

        if (!_hasLoadedOnce)
        {
            await LoadLatestAsync();
            return;
        }

        try
        {
            IsLoading = true;
            AppendNextPage();
            FeedbackMessage = $"{PendingMessages.Count} message(s) à traiter affiché(s).";
            FeedbackColor = Colors.LightGreen;
        }
        finally
        {
            IsLoading = false;
            RefreshCommands();
        }
    }

    private void AppendNextPage()
    {
        var skip = _pageIndex * PageSize;

        var chunk = _pendingCache.Skip(skip).Take(PageSize).ToList();
        foreach (var msg in chunk)
            PendingMessages.Add(msg);

        _pageIndex++;

        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(LoadMoreText));
    }

    private bool CanSendReply()
    {
        return !IsSubmitting
               && SelectedMessage is not null
               && !string.IsNullOrWhiteSpace(ReplyText);
    }

    private async Task SendReplyAsync()
    {
        if (!CanSendReply()) return;

        var target = SelectedMessage!;
        var reply = ReplyText.Trim();

        try
        {
            IsSubmitting = true;
            FeedbackMessage = "Envoi de la réponse…";
            FeedbackColor = Colors.Gold;

            if (!_sessionPrepared)
                await PrepareSessionAsync();

            // ✅ Date = maintenant (pas Today)
            var now = DateTime.Now;

            // ✅ Ton API renvoie "dateMessage" => on envoie "dateMessage"
            // Format ISO compatible
            var payload = new
            {
                id = target.Id,
                dateMessage = now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssK"),
                reponse = reply,
                etat = EtatTraite
            };

            var ok = await _apis.PostBoolAsync("/api/crud/messages/update", payload);

            if (!ok)
            {
                FeedbackMessage = "L'envoi a échoué.";
                FeedbackColor = Colors.OrangeRed;
                return;
            }

            // ✅ MAJ locale (utile si tu affiches un historique plus tard)
            target.Reponse = reply;
            target.Etat = EtatTraite;
            target.DateMessage = now;

            // ✅ retrait immédiat (on n’affiche que “A traiter”)
            RemoveFromLists(target);

            SelectedMessage = null;
            ReplyText = string.Empty;

            FeedbackMessage = "Réponse envoyée. Message marqué comme traité.";
            FeedbackColor = Colors.LightGreen;

            await ShowInfoAsync("Envoi", "Réponse envoyée. Le message est maintenant traité.");

            if (PendingMessages.Count == 0)
            {
                FeedbackMessage = "Aucun message à traiter.";
                FeedbackColor = Colors.LightGreen;
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            FeedbackMessage = "Session expirée. Veuillez vous reconnecter.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] 401 lors de l'envoi : {ex}");
        }
        catch (Exception ex)
        {
            FeedbackMessage = "Impossible d'envoyer la réponse.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] Erreur envoi : {ex}");
        }
        finally
        {
            IsSubmitting = false;
            RefreshCommands();
        }
    }

    private void RemoveFromLists(MessageEntry target)
    {
        _pendingCache.RemoveAll(m => m.Id == target.Id);

        var item = PendingMessages.FirstOrDefault(m => m.Id == target.Id);
        if (item is not null)
            PendingMessages.Remove(item);

        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(LoadMoreText));
    }

    private static DateTime GetMessageDate(MessageEntry m)
        => m.DateMessage?.ToLocalTime() ?? DateTime.MinValue;

    private static bool IsEtatATraiter(string? etat)
    {
        var e = (etat ?? string.Empty).Trim();

        // ✅ robuste sur casse + accents éventuels
        // (si ton backend renvoie toujours exactement "A traiter", tu peux simplifier)
        return string.Equals(e, EtatATraiter, StringComparison.OrdinalIgnoreCase)
               || string.Equals(e, "A traiter", StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshCommands()
    {
        (LoadLatestCommand as Command)?.ChangeCanExecute();
        (LoadMoreCommand as Command)?.ChangeCanExecute();
        (SendReplyCommand as Command)?.ChangeCanExecute();

        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(LoadMoreText));
    }

    private Task ShowInfoAsync(string title, string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
            await DialogService.DisplayAlertAsync(title, message, "OK"));
    }
}
