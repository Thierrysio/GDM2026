using Microsoft.Maui.ApplicationModel;
using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
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

    private string _commentsStatusMessage = "Chargement des commentaires";
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
            {
                RefreshLoadMoreState();
            }
        }
    }

    public bool HasMoreComments
    {
        get => _hasMoreComments;
        set
        {
            if (SetProperty(ref _hasMoreComments, value))
            {
                RefreshLoadMoreState();
            }
        }
    }

    public bool CanLoadMore
    {
        get => _canLoadMore;
        private set
        {
            if (SetProperty(ref _canLoadMore, value))
            {
                (LoadMoreCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public async Task InitializeAsync()
    {
        if (_commentsLoaded)
        {
            return;
        }

        await PrepareSessionAsync();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (IsCommentsLoading)
        {
            return;
        }

        IsRefreshing = true;
        _currentOffset = 0;
        HasMoreComments = false;
        Comments.Clear();

        await LoadNextBatchAsync(isInitial: true);
    }

    private async Task LoadNextBatchAsync(bool isInitial = false)
    {
        if (IsCommentsLoading)
        {
            return;
        }

        try
        {
            IsCommentsLoading = true;
            CommentsStatusMessage = isInitial
                ? "Chargement des commentaires"
                : "Chargement des commentaires suivants";

            var items = await FetchCommentsAsync(_currentOffset, PageSize).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var item in items)
                {
                    Comments.Add(item);
                }

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

    private async Task DeleteCommentAsync(CommentEntry? comment)
    {
        if (comment is null)
        {
            return;
        }

        var confirm = await ConfirmAsync("Suppression", "Supprimer ce commentaire ?");
        if (!confirm)
        {
            return;
        }

        try
        {
            IsCommentsLoading = true;
            await PrepareSessionAsync();

            var deleted = await TryDeleteCommentAsync(comment.Id).ConfigureAwait(false);
            if (!deleted)
            {
                CommentsStatusMessage = "La suppression du commentaire a échoué.";
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
            CommentsStatusMessage = "Impossible de supprimer le commentaire.";
            Debug.WriteLine($"[COMMENTS] Delete error: {ex}");
        }
        finally
        {
            IsCommentsLoading = false;
        }

        if (HasMoreComments)
        {
            await LoadNextBatchAsync();
        }
    }

    private async Task PrepareSessionAsync()
    {
        if (_sessionPrepared)
        {
            return;
        }

        try
        {
            await _sessionService.LoadAsync().ConfigureAwait(false);
            _apis.SetBearerToken(_sessionService.AuthToken);
            _sessionPrepared = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[COMMENTS] Session non préparée : {ex}");
        }
    }

    private async Task<List<CommentEntry>> FetchCommentsAsync(int skip, int take)
    {
        List<CommentEntry>? lastResult = null;
        Exception? lastError = null;

        var endpoints = new[]
        {
            $"/api/mobile/commentaires?skip={skip}&take={take}",
            $"/api/mobile/getCommentaires?skip={skip}&take={take}",
            $"/api/crud/commentaire/list?skip={skip}&take={take}",
            $"/api/crud/commentaires/list?skip={skip}&take={take}"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                var items = await _apis.GetListAsync<CommentEntry>(endpoint).ConfigureAwait(false);
                var sorted = OrderComments(items);
                var paged = sorted.Skip(skip).Take(take).ToList();

                if (paged.Count > 0)
                {
                    return paged;
                }

                if (lastResult == null)
                {
                    lastResult = paged.Count > 0 ? paged : sorted.Take(take).ToList();
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

    private static List<CommentEntry> OrderComments(List<CommentEntry> items)
    {
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
                var success = await _apis.PostBoolAsync(endpoint, new { id = commentId }).ConfigureAwait(false);
                if (success)
                {
                    return true;
                }
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
