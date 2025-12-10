using Microsoft.Maui.ApplicationModel;
using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class CommentsViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private string _commentText = string.Empty;
    private string _ratingText = string.Empty;
    private DateTime _selectedDate = DateTime.Today;
    private string _userName = string.Empty;
    private string _productName = string.Empty;
    private string _statusMessage = "Entrez un commentaire puis validez.";
    private Color _statusColor = Colors.Gold;
    private string _commentsStatusMessage = "Chargement des commentaires…";
    private bool _isRefreshing;
    private bool _isCommentsLoading;
    private bool _commentsLoaded;

    public CommentsViewModel()
    {
        SubmitCommand = new Command(async () => await SubmitAsync(), () => !IsBusy);
        RefreshCommentsCommand = new Command(async () => await LoadCommentsAsync(forceRefresh: true));
    }

    public ICommand SubmitCommand { get; }

    public ICommand RefreshCommentsCommand { get; }

    public ObservableCollection<CommentEntry> Comments { get; } = new();

    public string CommentText
    {
        get => _commentText;
        set => SetProperty(ref _commentText, value);
    }

    public string RatingText
    {
        get => _ratingText;
        set => SetProperty(ref _ratingText, value);
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set => SetProperty(ref _selectedDate, value);
    }

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    public string ProductName
    {
        get => _productName;
        set => SetProperty(ref _productName, value);
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

    public string CommentsStatusMessage
    {
        get => _commentsStatusMessage;
        set => SetProperty(ref _commentsStatusMessage, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public bool IsCommentsLoading
    {
        get => _isCommentsLoading;
        set => SetProperty(ref _isCommentsLoading, value);
    }

    public async Task InitializeAsync()
    {
        if (!_commentsLoaded)
        {
            await PrepareSessionAsync();
            await LoadCommentsAsync();
        }
    }

    private async Task SubmitAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        (SubmitCommand as Command)?.ChangeCanExecute();

        try
        {
            if (string.IsNullOrWhiteSpace(UserName)
                || string.IsNullOrWhiteSpace(ProductName)
                || string.IsNullOrWhiteSpace(CommentText)
                || string.IsNullOrWhiteSpace(RatingText))
            {
                StatusColor = Colors.OrangeRed;
                StatusMessage = "Merci de renseigner toutes les informations.";
                return;
            }

            StatusColor = Colors.LightGreen;
            StatusMessage = "Commentaire prêt à être envoyé (intégration API à réaliser).";
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsBusy = false;
                (SubmitCommand as Command)?.ChangeCanExecute();
            });
        }
    }

    private async Task PrepareSessionAsync()
    {
        try
        {
            await _sessionService.LoadAsync().ConfigureAwait(false);
            _apis.SetBearerToken(_sessionService.AuthToken);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[COMMENTS] Session non préparée : {ex}");
        }
    }

    private async Task LoadCommentsAsync(bool forceRefresh = false)
    {
        if (IsCommentsLoading)
        {
            return;
        }

        try
        {
            IsCommentsLoading = true;
            IsRefreshing = forceRefresh;
            CommentsStatusMessage = forceRefresh ? "Actualisation des commentaires…" : "Chargement des commentaires…";

            var items = await FetchCommentsAsync().ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Comments.Clear();
                foreach (var item in items)
                {
                    Comments.Add(item);
                }

                _commentsLoaded = true;
                CommentsStatusMessage = Comments.Count == 0
                    ? "Aucun commentaire à afficher."
                    : $"{Comments.Count} commentaire(s) chargé(s).";
            });
        }
        catch (TaskCanceledException)
        {
            CommentsStatusMessage = "Chargement annulé.";
        }
        catch (HttpRequestException ex)
        {
            CommentsStatusMessage = "Impossible de récupérer les commentaires.";
            Debug.WriteLine($"[COMMENTS] Erreur HTTP : {ex}");
        }
        catch (Exception ex)
        {
            CommentsStatusMessage = "Une erreur est survenue lors du chargement.";
            Debug.WriteLine($"[COMMENTS] Erreur inattendue : {ex}");
        }
        finally
        {
            IsCommentsLoading = false;
            IsRefreshing = false;
        }
    }

    private async Task<List<CommentEntry>> FetchCommentsAsync()
    {
        List<CommentEntry>? lastResult = null;
        Exception? lastError = null;

        var endpoints = new[]
        {
            "/api/mobile/commentaires",
            "/api/mobile/getCommentaires",
            "/api/crud/commentaire/list",
            "/api/crud/commentaires/list"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                var items = await _apis.GetListAsync<CommentEntry>(endpoint).ConfigureAwait(false);
                if (items.Count > 0)
                {
                    return items;
                }

                if (lastResult == null)
                {
                    lastResult = items;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                Debug.WriteLine($"[COMMENTS] Endpoint échoué '{endpoint}': {ex.Message}");
            }
        }

        if (lastResult != null)
        {
            return lastResult;
        }

        if (lastError != null)
        {
            throw lastError;
        }

        return new List<CommentEntry>();
    }
}
