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

    // ⚠️ Mets exactement les libellés backend si besoin
    private const string EtatATraiter = "A traiter";
    private const string EtatTraite = "traité";

    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionPrepared;
    private bool _isLoading;
    private bool _isSubmitting;

    private bool _hasLoadedOnce;
    private bool _isShowingOthers;

    private int _pendingPageIndex;
    private int _otherPageIndex;

    private string _feedbackMessage = "Cliquez sur « À traiter » pour charger les messages à traiter.";
    private Color _feedbackColor = Colors.Gold;

    private MessageEntry? _selectedMessage;
    private string _replyText = string.Empty;

    private List<MessageEntry> _pendingCache = new(); // Etat == A traiter
    private List<MessageEntry> _otherCache = new();   // Etat != A traiter

    public ObservableCollection<MessageEntry> DisplayMessages { get; } = new();

    public ICommand LoadPendingCommand { get; }
    public ICommand LoadOtherCommand { get; }
    public ICommand SendReplyCommand { get; }

    public MessagesViewModel()
    {
        LoadPendingCommand = new Command(async () => await ShowPendingAsync(), () => !IsLoading && !IsSubmitting);
        LoadOtherCommand = new Command(async () => await ShowOthersOrMoreAsync(), () => !IsLoading && !IsSubmitting && OtherButtonEnabled);

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

    public bool IsShowingOthers
    {
        get => _isShowingOthers;
        set
        {
            if (SetProperty(ref _isShowingOthers, value))
            {
                OnPropertyChanged(nameof(SelectionMode));
                OnPropertyChanged(nameof(OtherButtonText));
                OnPropertyChanged(nameof(PendingButtonText));
                OnPropertyChanged(nameof(IsReplySectionVisible));
                RefreshCommands();

                // en mode "autres", on ne doit rien modifier
                if (_isShowingOthers)
                {
                    SelectedMessage = null;
                    ReplyText = string.Empty;
                }
            }
        }
    }

    public SelectionMode SelectionMode => IsShowingOthers ? SelectionMode.None : SelectionMode.Single;

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
                OnPropertyChanged(nameof(IsReplySectionVisible));
            }
        }
    }

    public bool IsReplySectionVisible => SelectedMessage is not null && !IsShowingOthers;

    public string ReplyText
    {
        get => _replyText;
        set
        {
            if (SetProperty(ref _replyText, value))
                RefreshCommands();
        }
    }

    public string PendingButtonText => IsShowingOthers ? "À traiter" : "À traiter (10)";
    public string OtherButtonText
    {
        get
        {
            if (!IsShowingOthers)
                return "Autres";

            return HasMoreOthers ? "Voir plus (10)" : "Autres (fin)";
        }
    }

    private bool HasMorePending => _pendingCache.Count > DisplayMessages.Count && !IsShowingOthers;
    private bool HasMoreOthers => _otherCache.Count > DisplayMessages.Count && IsShowingOthers;

    private bool OtherButtonEnabled
        => !IsShowingOthers || HasMoreOthers; // si on n’est pas encore en "autres", le bouton sert à basculer

    public async Task InitializeAsync()
    {
        if (!_sessionPrepared)
            await PrepareSessionAsync();

        // ✅ Ne charge rien
        FeedbackMessage = "Cliquez sur « À traiter » pour charger les messages à traiter.";
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

    private async Task EnsureDataLoadedAsync()
    {
        if (_hasLoadedOnce)
            return;

        if (!_sessionPrepared)
            await PrepareSessionAsync();

        var items = await _apis.GetListAsync<MessageEntry>("/api/crud/messages/list");

        var list = items ?? new List<MessageEntry>();

        _pendingCache = list
            .Where(m => IsEtatATraiter(m.Etat))
            .OrderByDescending(GetMessageDate)
            .ToList();

        _otherCache = list
            .Where(m => !IsEtatATraiter(m.Etat))
            .OrderByDescending(GetMessageDate)
            .ToList();

        _hasLoadedOnce = true;
    }

    private async Task ShowPendingAsync()
    {
        if (IsLoading || IsSubmitting) return;

        try
        {
            IsLoading = true;
            IsShowingOthers = false;

            FeedbackMessage = "Chargement des messages à traiter…";
            FeedbackColor = Colors.Gold;

            _hasLoadedOnce = false; // on recharge la liste depuis l’API
            await EnsureDataLoadedAsync();

            _pendingPageIndex = 0;
            DisplayMessages.Clear();

            AppendPendingNextPage();

            FeedbackMessage = DisplayMessages.Count == 0
                ? "Aucun message à traiter."
                : $"{DisplayMessages.Count} message(s) à traiter affiché(s).";
            FeedbackColor = Colors.LightGreen;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            FeedbackMessage = "Accès refusé. Veuillez vous reconnecter.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] 401 : {ex}");
        }
        catch (Exception ex)
        {
            FeedbackMessage = "Impossible de charger les messages.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] load error : {ex}");
        }
        finally
        {
            IsLoading = false;
            RefreshCommands();
        }
    }

    private async Task ShowOthersOrMoreAsync()
    {
        if (IsLoading || IsSubmitting) return;

        try
        {
            IsLoading = true;

            // 1) si on n’est pas en mode "autres" => bascule + charge 10
            if (!IsShowingOthers)
            {
                IsShowingOthers = true;

                FeedbackMessage = "Chargement des autres messages…";
                FeedbackColor = Colors.Gold;

                _hasLoadedOnce = false; // on recharge depuis l’API
                await EnsureDataLoadedAsync();

                _otherPageIndex = 0;
                DisplayMessages.Clear();

                AppendOtherNextPage();

                FeedbackMessage = DisplayMessages.Count == 0
                    ? "Aucun autre message."
                    : $"{DisplayMessages.Count} autre(s) message(s) affiché(s).";
                FeedbackColor = Colors.LightGreen;

                return;
            }

            // 2) si déjà en mode autres => voir plus (10)
            AppendOtherNextPage();

            FeedbackMessage = $"{DisplayMessages.Count} autre(s) message(s) affiché(s).";
            FeedbackColor = Colors.LightGreen;
        }
        finally
        {
            IsLoading = false;
            RefreshCommands();
        }
    }

    private void AppendPendingNextPage()
    {
        var skip = _pendingPageIndex * PageSize;
        var chunk = _pendingCache.Skip(skip).Take(PageSize).ToList();

        foreach (var msg in chunk)
            DisplayMessages.Add(msg);

        _pendingPageIndex++;

        OnPropertyChanged(nameof(OtherButtonText));
        OnPropertyChanged(nameof(PendingButtonText));
    }

    private void AppendOtherNextPage()
    {
        var skip = _otherPageIndex * PageSize;
        var chunk = _otherCache.Skip(skip).Take(PageSize).ToList();

        foreach (var msg in chunk)
            DisplayMessages.Add(msg);

        _otherPageIndex++;

        OnPropertyChanged(nameof(OtherButtonText));
        OnPropertyChanged(nameof(PendingButtonText));
    }

    private bool CanSendReply()
    {
        return !IsSubmitting
               && !IsShowingOthers
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

            // ✅ date = maintenant
            var now = DateTime.Now;

            // ✅ API : elle renvoie dateMessage => on envoie dateMessage
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

            // ✅ MAJ locale
            target.Reponse = reply;
            target.Etat = EtatTraite;
            target.DateMessage = now;

            // ✅ disparaît : on n’affiche que "A traiter"
            RemoveFromPending(target);

            SelectedMessage = null;
            ReplyText = string.Empty;

            FeedbackMessage = "Réponse envoyée. Message traité.";
            FeedbackColor = Colors.LightGreen;

            await ShowInfoAsync("Envoi", "Réponse envoyée. Le message est maintenant traité.");

            if (DisplayMessages.Count == 0)
            {
                FeedbackMessage = "Aucun message à traiter.";
                FeedbackColor = Colors.LightGreen;
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            FeedbackMessage = "Session expirée. Veuillez vous reconnecter.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] 401 send : {ex}");
        }
        catch (Exception ex)
        {
            FeedbackMessage = "Impossible d'envoyer la réponse.";
            FeedbackColor = Colors.OrangeRed;
            Debug.WriteLine($"[MESSAGES] send error : {ex}");
        }
        finally
        {
            IsSubmitting = false;
            RefreshCommands();
        }
    }

    private void RemoveFromPending(MessageEntry target)
    {
        _pendingCache.RemoveAll(m => m.Id == target.Id);

        var item = DisplayMessages.FirstOrDefault(m => m.Id == target.Id);
        if (item is not null)
            DisplayMessages.Remove(item);

        OnPropertyChanged(nameof(OtherButtonText));
        OnPropertyChanged(nameof(PendingButtonText));
    }

    private static DateTime GetMessageDate(MessageEntry m)
        => m.DateMessage?.ToLocalTime() ?? DateTime.MinValue;

    private static bool IsEtatATraiter(string? etat)
        => string.Equals((etat ?? "").Trim(), EtatATraiter, StringComparison.OrdinalIgnoreCase);

    private void RefreshCommands()
    {
        (LoadPendingCommand as Command)?.ChangeCanExecute();
        (LoadOtherCommand as Command)?.ChangeCanExecute();
        (SendReplyCommand as Command)?.ChangeCanExecute();

        OnPropertyChanged(nameof(SelectionMode));
        OnPropertyChanged(nameof(OtherButtonText));
        OnPropertyChanged(nameof(PendingButtonText));
    }

    private Task ShowInfoAsync(string title, string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
            await DialogService.DisplayAlertAsync(title, message, "OK"));
    }
}
