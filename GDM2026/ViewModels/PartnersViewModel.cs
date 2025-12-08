using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class PartnersViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();
    private bool _sessionPrepared;
    private bool _hasLoaded;
    private bool _isRefreshing;
    private string _statusMessage = "Chargement des partenaires…";

    public PartnersViewModel()
    {
        RefreshCommand = new Command(async () => await LoadPartnersAsync(true));
        OpenWebsiteCommand = new Command<string?>(async url => await OpenWebsiteAsync(url));
    }

    public ObservableCollection<Partner> Partners { get; } = new();

    public ICommand RefreshCommand { get; }

    public ICommand OpenWebsiteCommand { get; }

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

    public async Task InitializeAsync()
    {
        if (!_sessionPrepared)
        {
            await PrepareSessionAsync();
        }

        if (!_hasLoaded)
        {
            await LoadPartnersAsync();
        }
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
            Debug.WriteLine($"[PARTNERS] Session non préparée : {ex}");
            _sessionPrepared = true;
        }
    }

    private async Task LoadPartnersAsync(bool forceRefresh = false)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            IsRefreshing = forceRefresh;
            StatusMessage = forceRefresh ? "Actualisation des partenaires…" : "Chargement des partenaires…";

            var partners = await FetchPartnersAsync();

            Partners.Clear();
            foreach (var partner in partners)
            {
                Partners.Add(partner);
            }

            _hasLoaded = true;

            StatusMessage = Partners.Count == 0
                ? "Aucun partenaire à afficher pour le moment."
                : $"{Partners.Count} partenaire(s) chargé(s).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Chargement annulé.";
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = "Impossible de récupérer les partenaires.";
            Debug.WriteLine($"[PARTNERS] Erreur HTTP : {ex}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Une erreur est survenue lors du chargement.";
            Debug.WriteLine($"[PARTNERS] Erreur inattendue : {ex}");
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    private async Task<List<Partner>> FetchPartnersAsync()
    {
        List<Partner>? partners = null;
        Exception? lastError = null;

        var endpoints = new[]
        {
            "/api/mobile/partenaires",
            "/api/crud/partenaire/list",
            "/api/crud/partenaires/list"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                partners = await _apis.GetListAsync<Partner>(endpoint).ConfigureAwait(false);
                if (partners.Count > 0)
                {
                    return partners;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                Debug.WriteLine($"[PARTNERS] Endpoint échoué '{endpoint}': {ex.Message}");
            }
        }

        if (partners != null)
        {
            return partners;
        }

        if (lastError != null)
        {
            throw lastError;
        }

        return new List<Partner>();
    }

    private static async Task OpenWebsiteAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (Uri.TryCreate($"https://{url.TrimStart('/')}", UriKind.Absolute, out var httpsUri))
            {
                uri = httpsUri;
            }
        }

        if (uri != null)
        {
            try
            {
                await Launcher.Default.TryOpenAsync(uri);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PARTNERS] Impossible d'ouvrir {url} : {ex}");
            }
        }
    }
}
