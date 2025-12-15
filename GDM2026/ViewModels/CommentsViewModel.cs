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
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private string _commentsStatusMessage = "Chargement des commentaires…";
    private bool _isRefreshing;
    private bool _isCommentsLoading;
    private bool _commentsLoaded;
    private bool _isDeleting;

    public CommentsViewModel()
    {
        RefreshCommentsCommand = new Command(async () => await LoadCommentsAsync(forceRefresh: true));
        DeleteCommentCommand = new Command<CommentEntry>(async entry => await DeleteCommentAsync(entry), CanDeleteComment);
    }

    public ICommand RefreshCommentsCommand { get; }

    public ICommand DeleteCommentCommand { get; }

    public ObservableCollection<CommentEntry> Comments { get; } = new();

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

    public bool IsDeleting
    {
        get => _isDeleting;
        private set => SetProperty(ref _isDeleting, value);
    }

    public async Task InitializeAsync()
    {
        if (!_commentsLoaded)
        {
            await PrepareSessionAsync();
            await LoadCommentsAsync();
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
                foreach (var item in GetLatestComments(items))
                {
                    Comments.Add(item);
                }

                _commentsLoaded = true;
                CommentsStatusMessage = Comments.Count == 0
                    ? "Aucun commentaire à afficher."
                    : $"{Comments.Count} dernier(s) commentaire(s) chargé(s).";
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
            RefreshCommands();
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

                lastResult ??= items;
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

    private List<CommentEntry> GetLatestComments(List<CommentEntry> items)
    {
        return items
            .OrderByDescending(c => c.DateCommentaire ?? DateTime.MinValue)
            .ThenByDescending(c => c.Id)
            .Take(5)
            .ToList();
    }

    private bool CanDeleteComment(CommentEntry? entry) => entry is not null && !IsCommentsLoading && !IsDeleting;

    private void RefreshCommands()
    {
        (DeleteCommentCommand as Command<CommentEntry>)?.ChangeCanExecute();
    }

    private async Task DeleteCommentAsync(CommentEntry? entry)
    {
        if (entry is null || IsDeleting)
        {
            return;
        }

        var shell = Shell.Current;
        if (shell is not null)
        {
            var confirmed = await shell.DisplayAlert(
                "Suppression",
                $"Supprimer définitivement le commentaire #{entry.Id} ?",
                "Supprimer",
                "Annuler");

            if (!confirmed)
            {
                return;
            }
        }

        try
        {
            IsDeleting = true;
            RefreshCommands();
            CommentsStatusMessage = "Suppression en cours…";

            await PrepareSessionAsync();

            var deleted = await _apis.DeleteAsync($"/commentaires/{entry.Id}");
            if (deleted)
            {
                await MainThread.InvokeOnMainThreadAsync(() => Comments.Remove(entry));
                CommentsStatusMessage = "Commentaire supprimé.";
            }
            else
            {
                CommentsStatusMessage = "Impossible de supprimer le commentaire.";
            }
        }
        catch (Exception ex)
        {
            CommentsStatusMessage = "Erreur lors de la suppression.";
            Debug.WriteLine($"[COMMENTS] Suppression échouée : {ex}");
        }
        finally
        {
            IsDeleting = false;
            RefreshCommands();
        }
    }
}
