using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
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

public class PartnersViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionPrepared;
    private bool _hasLoaded;

    private bool _isLoading;
    private string _statusMessage = "Cliquez sur « Modifier / Supprimer » pour charger les partenaires.";

    private bool _isSelectionVisible;

    private Partner? _selectedPartner;

    private string _editPartnerName = string.Empty;
    private string _editPartnerWebsite = string.Empty;

    private string _newPartnerName = string.Empty;
    private string _newPartnerWebsite = string.Empty;

    public PartnersViewModel()
    {
        ToggleEditModeCommand = new Command(async () => await ToggleEditModeAsync(), () => !IsLoading && !IsBusy);

        CreateCommand = new Command(async () => await CreateAsync(), CanCreate);
        UpdateCommand = new Command(async () => await UpdateAsync(), CanUpdate);
        DeleteCommand = new Command(async () => await DeleteAsync(), CanDelete);

        OpenWebsiteCommand = new Command<string?>(async url => await OpenWebsiteAsync(url));
    }

    public ObservableCollection<Partner> Partners { get; } = new();

    public ICommand ToggleEditModeCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenWebsiteCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
                RefreshCommands();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSelectionVisible
    {
        get => _isSelectionVisible;
        set
        {
            if (SetProperty(ref _isSelectionVisible, value))
            {
                OnPropertyChanged(nameof(EditModeButtonText));
                RefreshCommands();
            }
        }
    }

    public string EditModeButtonText => IsSelectionVisible ? "Masquer" : "Modifier / Supprimer";

    public Partner? SelectedPartner
    {
        get => _selectedPartner;
        set
        {
            if (SetProperty(ref _selectedPartner, value))
            {
                EditPartnerName = value?.DisplayName ?? string.Empty;
                EditPartnerWebsite = value?.Website ?? string.Empty;
                OnPropertyChanged(nameof(SelectedPartnerLabel));
                RefreshCommands();
            }
        }
    }

    public string SelectedPartnerLabel => SelectedPartner is null
        ? "Aucun partenaire sélectionné."
        : $"#{SelectedPartner.Id} — {SelectedPartner.DisplayName}";

    public string EditPartnerName
    {
        get => _editPartnerName;
        set
        {
            if (SetProperty(ref _editPartnerName, value))
                RefreshCommands();
        }
    }

    public string EditPartnerWebsite
    {
        get => _editPartnerWebsite;
        set
        {
            if (SetProperty(ref _editPartnerWebsite, value))
                RefreshCommands();
        }
    }

    public string NewPartnerName
    {
        get => _newPartnerName;
        set
        {
            if (SetProperty(ref _newPartnerName, value))
                RefreshCommands();
        }
    }

    public string NewPartnerWebsite
    {
        get => _newPartnerWebsite;
        set
        {
            if (SetProperty(ref _newPartnerWebsite, value))
                RefreshCommands();
        }
    }

    // appelé par la page : NE charge rien
    public async Task OnPageAppearingAsync()
    {
        if (!_sessionPrepared)
            await PrepareSessionAsync();

        StatusMessage = "Cliquez sur « Modifier / Supprimer » pour charger les partenaires.";
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

    private async Task ToggleEditModeAsync()
    {
        IsSelectionVisible = !IsSelectionVisible;

        if (IsSelectionVisible)
        {
            await LoadPartnersAsync(forceReload: true);
        }
        else
        {
            SelectedPartner = null;
            EditPartnerName = string.Empty;
            EditPartnerWebsite = string.Empty;
        }
    }

    private async Task LoadPartnersAsync(bool forceReload = false)
    {
        if (IsBusy || IsLoading)
            return;

        if (!forceReload && _hasLoaded)
            return;

        try
        {
            IsLoading = true;
            IsBusy = true;

            StatusMessage = "Chargement des partenaires…";

            if (!_sessionPrepared)
                await PrepareSessionAsync();

            var partners = await _apis.GetListAsync<Partner>("/api/crud/partenaires/list").ConfigureAwait(false);
            partners ??= new List<Partner>();

            Partners.Clear();
            foreach (var partner in partners)
                Partners.Add(partner);

            _hasLoaded = true;

            StatusMessage = Partners.Count == 0
                ? "Aucun partenaire à afficher."
                : $"{Partners.Count} partenaire(s) chargé(s).";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            StatusMessage = "Accès refusé. Veuillez vous reconnecter.";
            Debug.WriteLine($"[PARTNERS] 401 : {ex}");
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de récupérer les partenaires.";
            Debug.WriteLine($"[PARTNERS] load error : {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    private bool CanCreate()
        => !IsBusy
           && !IsLoading
           && !string.IsNullOrWhiteSpace(NewPartnerName);

    private async Task CreateAsync()
    {
        if (!CanCreate())
        {
            StatusMessage = "Renseignez le nom du partenaire.";
            return;
        }

        try
        {
            IsBusy = true;
            IsLoading = true;
            RefreshCommands();

            if (!_sessionPrepared)
                await PrepareSessionAsync();

            var payload = new
            {
                nom = NewPartnerName.Trim(),
                url = string.IsNullOrWhiteSpace(NewPartnerWebsite) ? null : NewPartnerWebsite.Trim()
                // logo optionnel : à gérer plus tard (upload)
            };

            var ok = await _apis.PostBoolAsync("/api/crud/partenaires/create", payload).ConfigureAwait(false);
            if (!ok)
            {
                StatusMessage = "Création échouée.";
                return;
            }

            StatusMessage = "Partenaire créé.";
            await ShowInfoAsync("Création", "Partenaire créé avec succès.");

            NewPartnerName = string.Empty;
            NewPartnerWebsite = string.Empty;

            // si on est en mode édition, recharge la liste
            if (IsSelectionVisible)
                await LoadPartnersAsync(forceReload: true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de créer le partenaire.";
            Debug.WriteLine($"[PARTNERS] create error : {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    private bool CanUpdate()
        => !IsBusy
           && !IsLoading
           && IsSelectionVisible
           && SelectedPartner is not null
           && !string.IsNullOrWhiteSpace(EditPartnerName);

    private async Task UpdateAsync()
    {
        if (!CanUpdate())
        {
            StatusMessage = "Sélectionnez un partenaire et renseignez au moins son nom.";
            return;
        }

        var partner = SelectedPartner!;
        var name = EditPartnerName.Trim();
        var website = string.IsNullOrWhiteSpace(EditPartnerWebsite) ? null : EditPartnerWebsite.Trim();

        var confirm = await ConfirmAsync("Mise à jour", $"Mettre à jour « {partner.DisplayName} » ?");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            IsLoading = true;
            RefreshCommands();

            if (!_sessionPrepared)
                await PrepareSessionAsync();

            var payload = new
            {
                id = partner.Id,
                nom = name,
                url = website,
                site_web = website
            };

            var ok = await _apis.PostBoolAsync("/api/crud/partenaires/update", payload).ConfigureAwait(false);
            if (!ok)
            {
                StatusMessage = "La mise à jour a échoué.";
                return;
            }

            StatusMessage = "Partenaire mis à jour.";
            await ShowInfoAsync("Mise à jour", "Partenaire mis à jour avec succès.");

            await LoadPartnersAsync(forceReload: true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de mettre à jour le partenaire.";
            Debug.WriteLine($"[PARTNERS] update error : {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    private bool CanDelete()
        => !IsBusy
           && !IsLoading
           && IsSelectionVisible
           && SelectedPartner is not null;

    private async Task DeleteAsync()
    {
        if (!CanDelete())
        {
            StatusMessage = "Sélectionnez un partenaire à supprimer.";
            return;
        }

        var partner = SelectedPartner!;

        var confirm = await ConfirmAsync("Suppression", $"Supprimer « {partner.DisplayName} » ?");
        if (!confirm) return;

        try
        {
            IsBusy = true;
            IsLoading = true;
            RefreshCommands();

            if (!_sessionPrepared)
                await PrepareSessionAsync();

            var payload = new { id = partner.Id };

            var ok = await _apis.PostBoolAsync("/api/crud/partenaires/delete", payload).ConfigureAwait(false);
            if (!ok)
            {
                StatusMessage = "La suppression a échoué.";
                return;
            }

            Partners.Remove(partner);

            SelectedPartner = null;
            EditPartnerName = string.Empty;
            EditPartnerWebsite = string.Empty;

            StatusMessage = "Partenaire supprimé.";
            await ShowInfoAsync("Suppression", "Partenaire supprimé avec succès.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Impossible de supprimer le partenaire.";
            Debug.WriteLine($"[PARTNERS] delete error : {ex}");
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
            RefreshCommands();
        }
    }

    private void RefreshCommands()
    {
        (ToggleEditModeCommand as Command)?.ChangeCanExecute();
        (CreateCommand as Command)?.ChangeCanExecute();
        (UpdateCommand as Command)?.ChangeCanExecute();
        (DeleteCommand as Command)?.ChangeCanExecute();
        OnPropertyChanged(nameof(SelectedPartnerLabel));
    }

    private static async Task OpenWebsiteAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (Uri.TryCreate($"https://{url.TrimStart('/')}", UriKind.Absolute, out var httpsUri))
                uri = httpsUri;
        }

        if (uri is null)
            return;

        try
        {
            await Launcher.Default.TryOpenAsync(uri);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PARTNERS] Impossible d'ouvrir {url} : {ex}");
        }
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

    private Task ShowInfoAsync(string title, string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null) return;
            await page.DisplayAlert(title, message, "OK");
        });
    }
}
