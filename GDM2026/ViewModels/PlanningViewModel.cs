using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using GDM2026.Models;
using GDM2026.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Newtonsoft.Json;

namespace GDM2026.ViewModels;

public class PlanningViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionPrepared;
    private bool _isLoading;
    private string _statusMessage = "Chargez le planning pour commencer.";

    private Planning? _selectedPlanning;
    private DateTime _newJour = DateTime.Today;
    private TimeSpan _newHeureDebut = TimeSpan.Zero;
    private TimeSpan _newHeureFin = TimeSpan.Zero;

    private DateTime _editJour = DateTime.Today;
    private TimeSpan _editHeureDebut = TimeSpan.Zero;
    private TimeSpan _editHeureFin = TimeSpan.Zero;

    public PlanningViewModel()
    {
        Plannings = new ObservableCollection<Planning>();

        RefreshCommand = new Command(async () => await LoadPlanningsAsync(), () => !IsLoading);
        CreateCommand = new Command(async () => await CreateAsync(), () => !IsLoading);
        UpdateCommand = new Command(async () => await UpdateAsync(), CanUpdate);
        DeleteCommand = new Command(async () => await DeleteAsync(), CanDelete);
    }

    public ObservableCollection<Planning> Plannings { get; }

    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                (RefreshCommand as Command)?.ChangeCanExecute();
                (CreateCommand as Command)?.ChangeCanExecute();
                RefreshEditCommands();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Planning? SelectedPlanning
    {
        get => _selectedPlanning;
        set
        {
            if (SetProperty(ref _selectedPlanning, value))
            {
                ApplySelection(value);
                RefreshEditCommands();
                OnPropertyChanged(nameof(SelectedPlanningLabel));
            }
        }
    }

    public string SelectedPlanningLabel => SelectedPlanning is null
        ? "Aucun créneau sélectionné."
        : $"#{SelectedPlanning.Id} — {SelectedPlanning.DisplayDate}";

    public DateTime NewJour
    {
        get => _newJour;
        set => SetProperty(ref _newJour, value);
    }

    public TimeSpan NewHeureDebut
    {
        get => _newHeureDebut;
        set => SetProperty(ref _newHeureDebut, value);
    }

    public TimeSpan NewHeureFin
    {
        get => _newHeureFin;
        set => SetProperty(ref _newHeureFin, value);
    }

    public DateTime EditJour
    {
        get => _editJour;
        set
        {
            if (SetProperty(ref _editJour, value))
                RefreshEditCommands();
        }
    }

    public TimeSpan EditHeureDebut
    {
        get => _editHeureDebut;
        set
        {
            if (SetProperty(ref _editHeureDebut, value))
                RefreshEditCommands();
        }
    }

    public TimeSpan EditHeureFin
    {
        get => _editHeureFin;
        set
        {
            if (SetProperty(ref _editHeureFin, value))
                RefreshEditCommands();
        }
    }

    public async Task OnPageAppearingAsync()
    {
        if (!_sessionPrepared)
        {
            await EnsureSessionAsync();
        }

        await LoadPlanningsAsync();
    }

    private async Task EnsureSessionAsync()
    {
        var hasSession = await _sessionService.LoadAsync().ConfigureAwait(false);
        _apis.SetBearerToken(_sessionService.AuthToken);
        _sessionPrepared = hasSession;
    }

    private async Task LoadPlanningsAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Chargement du planning…";

            await EnsureSessionAsync();

            var items = await _apis.GetListAsync<Planning>("/planning/list").ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Plannings.Clear();
                foreach (var item in items.OrderBy(p => p.Jour ?? DateTime.MaxValue))
                {
                    Plannings.Add(item);
                }

                StatusMessage = Plannings.Count == 0
                    ? "Aucun créneau planifié."
                    : $"{Plannings.Count} créneau(x) chargé(s).";
            });
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "Chargement annulé.";
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[PLANNING] HTTP error: {ex.Message}");
            StatusMessage = "Impossible de récupérer le planning.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PLANNING] Unexpected error: {ex}");
            StatusMessage = "Une erreur est survenue.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CreateAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Création du créneau…";

            await EnsureSessionAsync();

            var payload = new
            {
                jour = NewJour.ToString("yyyy-MM-dd"),
                heureDebut = FormatTime(NewHeureDebut),
                heureFin = FormatTime(NewHeureFin)
            };

            var response = await _apis.PostAsync<object, PlanningResponse>("/planning/create", payload)
                .ConfigureAwait(false);

            await LoadPlanningsAsync();
            StatusMessage = response?.Message ?? "Créneau créé.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Debug.WriteLine($"[PLANNING] Create error: {ex.Message}");
            StatusMessage = "La création a échoué.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanUpdate() => !IsLoading && SelectedPlanning != null;

    private async Task UpdateAsync()
    {
        if (SelectedPlanning is null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Mise à jour du créneau…";

            await EnsureSessionAsync();

            var payload = new
            {
                id = SelectedPlanning.Id,
                jour = EditJour.ToString("yyyy-MM-dd"),
                heureDebut = FormatTime(EditHeureDebut),
                heureFin = FormatTime(EditHeureFin)
            };

            var response = await _apis.PostAsync<object, PlanningResponse>("/planning/update", payload)
                .ConfigureAwait(false);

            await LoadPlanningsAsync();
            StatusMessage = response?.Message ?? "Créneau mis à jour.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Debug.WriteLine($"[PLANNING] Update error: {ex.Message}");
            StatusMessage = "La mise à jour a échoué.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanDelete() => !IsLoading && SelectedPlanning != null;

    private async Task DeleteAsync()
    {
        if (SelectedPlanning is null) return;

        var confirm = await ConfirmAsync("Suppression", "Supprimer ce créneau ?");
        if (!confirm) return;

        try
        {
            IsLoading = true;
            StatusMessage = "Suppression du créneau…";

            await EnsureSessionAsync();

            var payload = new { id = SelectedPlanning.Id };
            var response = await _apis.PostAsync<object, PlanningResponse>("/planning/delete", payload)
                .ConfigureAwait(false);

            await LoadPlanningsAsync();
            SelectedPlanning = null;
            StatusMessage = response?.Message ?? "Créneau supprimé.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Debug.WriteLine($"[PLANNING] Delete error: {ex.Message}");
            StatusMessage = "La suppression a échoué.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplySelection(Planning? planning)
    {
        if (planning is null)
        {
            return;
        }

        EditJour = planning.Jour?.Date ?? DateTime.Today;
        EditHeureDebut = planning.HeureDebutSpan ?? TimeSpan.Zero;
        EditHeureFin = planning.HeureFinSpan ?? TimeSpan.Zero;
    }

    private void RefreshEditCommands()
    {
        (UpdateCommand as Command)?.ChangeCanExecute();
        (DeleteCommand as Command)?.ChangeCanExecute();
    }

    private static string FormatTime(TimeSpan time) => time.ToString(@"hh\:mm\:ss");

    private Task<bool> ConfirmAsync(string title, string message)
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows?.FirstOrDefault()?.Page;
            if (page is null) return true;
            return await page.DisplayAlert(title, message, "Oui", "Non");
        });
    }

    private sealed class PlanningResponse
    {
        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("data")]
        public Planning? Data { get; set; }
    }
}
