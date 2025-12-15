using Microsoft.Maui.ApplicationModel;
using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class CommentsViewModel : BaseViewModel
{
    private const int PageSize = 5;

    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private string _commentsStatusMessage = "Chargement des commentaires…";
    private bool _isRefreshing;
    private bool _isCommentsLoading;
    private bool _commentsLoaded;
    private bool _hasMoreComments;
    private bool _canLoadMore;
    private bool _sessionPrepared;
    private int _currentOffset;

    public CommentsViewModel()
    {
        RefreshCommentsCommand = new Command(async () => await ReloadAsync());
        LoadMoreCommand = new Command(async () => await LoadNextBatchAsync(), () => CanLoadMore);
        DeleteCommentCommand = new Command<CommentEntry>(async entry => await DeleteCommentAsync(entry));
    }

    public ObservableCollection<CommentEntry> Comments { get; } = new();

    public ICommand RefreshCommentsCommand { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand DeleteCommentCommand { get; }

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
        set
        {
            if (SetProperty(ref _isCommentsLoading, value))
                RefreshLoadMoreState();
        }
    }

    public bool HasMoreComments
    {
        get => _hasMoreComments;
        set
        {
            if (SetProperty(ref _hasMoreComments, value))
                RefreshLoadMoreState();
        }
    }

    public bool CanLoadMore
    {
        get => _canLoadMore;
        private set
        {
            if (SetProperty(ref _canLoadMore, value))
                (LoadMoreCommand as Command)?.ChangeCanExecute();
        }
    }

    public async Task InitializeAsync()
    {
        if (_commentsLoaded) return;

        await PrepareSessionAsync();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (IsCommentsLoading) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            IsRefreshing = true;
            _currentOffset = 0;
            HasMoreComments = false;
            Comments.Clear();
            CommentsStatusMessage = "Chargement des commentaires…";
        });

        await LoadNextBatchAsync(isInitial: true);
    }

    private async Task LoadNextBatchAsync(bool isInitial = false)
    {
        if (IsCommentsLoading) return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsCommentsLoading = true;
                CommentsStatusMessage = isInitial
                    ? "Chargement des commentaires…"
                    : "Chargement des commentaires suivants…";
            });

            var items = await FetchCommentsAsync(_currentOffset, PageSize);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var item in items)
                    Comments.Add(item);

                _currentOffset += items.Count;
                HasMoreComments = items.Count == PageSize;
                _commentsLoaded = true;

                CommentsStatusMessage = Comments.Count == 0
                    ? "Aucun commentaire à afficher."
                    : $"{Comments.Count} commentaire(s) chargé(s).";
            });
        }
        catch (TaskCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                CommentsStatusMessage = "Chargement annulé."
            );
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[COMMENTS] Erreur HTTP : {ex}");
            await MainThread.InvokeOnMainThreadAsync(() =>
                CommentsStatusMessage = "Impossible de récupérer les commentaires."
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[COMMENTS] Erreur inattendue : {ex}");
            await MainThread.InvokeOnMainThreadAsync(() =>
                CommentsStatusMessage = "Une erreur est survenue lors du chargement."
            );
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsCommentsLoading = false;
                IsRefreshing = false;
            });
        }
    }

    private async Task DeleteCommentAsync(CommentEntry? comment)
    {
        if (comment is null) return;

        var confirm = await ConfirmAsync("Suppression", "Supprimer ce commentaire ?");
        if (!confirm) return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsCommentsLoading = true);

            await PrepareSessionAsync();

            var deleted = await TryDeleteCommentAsync(comment.Id);
            if (!deleted)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    CommentsStatusMessage = "La suppression du commentaire a échoué."
                );
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Comments.Remove(comment);
                _currentOffset = Math.Max(Comments.Count, _currentOffset - 1);

                CommentsStatusMessage = Comments.Count == 0
                    ? "Aucun commentaire à afficher."
                    : $"{Comments.Count} commentaire(s) chargé(s).";
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[COMMENTS] Delete error: {ex}");
            await MainThread.InvokeOnMainThreadAsync(() =>
                CommentsStatusMessage = "Impossible de supprimer le commentaire."
            );
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsCommentsLoading = false);
        }

        if (HasMoreComments)
            await LoadNextBatchAsync();
    }

    private async Task PrepareSessionAsync()
    {
        if (_sessionPrepared) return;

        try
        {
            await _sessionService.LoadAsync();

            // Important : évite un SetBearerToken(null) si ton service n’a rien chargé
            if (!string.IsNullOrWhiteSpace(_sessionService.AuthToken))
            {
                _apis.SetBearerToken(_sessionService.AuthToken);
                _sessionPrepared = true;
            }
            else
            {
                Debug.WriteLine("[COMMENTS] AuthToken vide : session non prête.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[COMMENTS] Session non préparée : {ex}");
        }
    }

    private async Task<List<CommentEntry>> FetchCommentsAsync(int skip, int take)
    {
        var endpoint = $"/commentaires/getCommentaires?skip={skip}&take={take}";

        // Tolérance : GetListAsync peut renvoyer null selon ton JSON / ta désérialisation
        var items = await _apis.GetListAsync<CommentEntry>(endpoint);
        items ??= new List<CommentEntry>();

        var sorted = OrderComments(items);

        // On fait SIMPLE : si l’API gère skip/take, sorted contient déjà la page.
        // Si l’API ignore les params et renvoie tout, on découpe côté client.
        if (sorted.Count > take && skip >= 0)
            return sorted.Skip(skip).Take(take).ToList();

        return sorted.Take(take).ToList();
    }

    private static List<CommentEntry> OrderComments(List<CommentEntry> items)
    {
        items ??= new List<CommentEntry>();

        return items
            .OrderByDescending(c => c.DateCommentaire ?? DateTime.MinValue)
            .ThenByDescending(c => c.Id)
            .ToList();
    }

    private async Task<bool> TryDeleteCommentAsync(int commentId)
    {
        var deleteEndpoints = new[]
        {
            "/api/crud/commentaire/delete",
            "/api/crud/commentaires/delete",
            "/api/mobile/commentaire/delete",
            "/api/mobile/commentaires/delete"
        };

        foreach (var endpoint in deleteEndpoints)
        {
            try
            {
                var success = await _apis.PostBoolAsync(endpoint, new { id = commentId });
                if (success) return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Debug.WriteLine($"[COMMENTS] Delete endpoint échoué '{endpoint}': {ex.Message}");
            }
        }

        return false;
    }

    private void RefreshLoadMoreState()
    {
        CanLoadMore = HasMoreComments && !IsCommentsLoading;
    }

    private Task<bool> ConfirmAsync(string title, string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null) return true;
            return await page.DisplayAlert(title, message, "Oui", "Non");
        });
    }
}
