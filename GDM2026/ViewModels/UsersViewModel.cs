using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
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

public class UsersViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionPrepared;
    private bool _hasLoaded;
    private bool _isRefreshing;
    private string _statusMessage = "Chargement des utilisateurs…";
    private string _searchText = string.Empty;

    public UsersViewModel()
    {
        Users = new ObservableCollection<User>();
        FilteredUsers = new ObservableCollection<User>();
        RefreshCommand = new Command(async () => await LoadUsersAsync(forceRefresh: true));
    }

    public ObservableCollection<User> Users { get; }

    public ObservableCollection<User> FilteredUsers { get; }

    public ICommand RefreshCommand { get; }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public async Task InitializeAsync()
    {
        if (!_sessionPrepared)
        {
            await PrepareSessionAsync();
        }

        if (!_hasLoaded)
        {
            await LoadUsersAsync();
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
            Debug.WriteLine($"[USERS] Impossible de charger la session : {ex}");
        }
        finally
        {
            _sessionPrepared = true;
        }
    }

    private async Task LoadUsersAsync(bool forceRefresh = false)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            IsRefreshing = forceRefresh;
            StatusMessage = forceRefresh ? "Actualisation des utilisateurs…" : "Chargement des utilisateurs…";

            var items = await FetchUsersAsync().ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Users.Clear();
                foreach (var item in items)
                {
                    Users.Add(item);
                }

                _hasLoaded = true;
                ApplyFilter();

                if (!Users.Any())
                {
                    StatusMessage = "Aucun utilisateur à afficher.";
                }
                else if (string.IsNullOrWhiteSpace(SearchText))
                {
                    StatusMessage = $"{Users.Count} utilisateur(s) chargé(s).";
                }
            });
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "Chargement annulé.";
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = "Impossible de récupérer les utilisateurs.";
            Debug.WriteLine($"[USERS] Erreur HTTP : {ex}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Une erreur est survenue lors du chargement.";
            Debug.WriteLine($"[USERS] Erreur inattendue : {ex}");
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    private async Task<List<User>> FetchUsersAsync()
    {
        List<User>? lastResult = null;
        Exception? lastError = null;

        var endpoints = new[]
        {
            "/api/mobile/GetListUsers",
            "/api/mobile/getListUsers",
            "/api/mobile/users",
            "/api/crud/users/list",
            "/api/crud/utilisateur/list",
            "/api/crud/utilisateurs/list"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                var data = await _apis.GetListAsync<User>(endpoint).ConfigureAwait(false);
                if (data.Count > 0)
                {
                    return data;
                }

                if (lastResult == null)
                {
                    lastResult = data;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                Debug.WriteLine($"[USERS] Endpoint échoué '{endpoint}': {ex.Message}");
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

        return new List<User>();
    }

    private void ApplyFilter()
    {
        IEnumerable<User> source = Users;
        var query = SearchText?.Trim();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.ToLowerInvariant();
            source = source.Where(u =>
                (u.DisplayName?.ToLowerInvariant().Contains(normalized) ?? false) ||
                (u.Email?.ToLowerInvariant().Contains(normalized) ?? false) ||
                (u.UserIdentifier?.ToLowerInvariant().Contains(normalized) ?? false) ||
                (u.RolesSummary?.ToLowerInvariant().Contains(normalized) ?? false));
        }

        FilteredUsers.Clear();
        foreach (var item in source)
        {
            FilteredUsers.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            StatusMessage = FilteredUsers.Count == 0
                ? "Aucun utilisateur ne correspond à cette recherche."
                : $"{FilteredUsers.Count} résultat(s) pour \"{query}\".";
        }
        else if (Users.Count > 0)
        {
            StatusMessage = $"{Users.Count} utilisateur(s) chargé(s).";
        }
    }
}
