using GDM2026.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GDM2026.ViewModels;

public class ReservationsViewModel : BaseViewModel
{
    private readonly Apis _apis = new();
    private readonly SessionService _sessionService = new();

    private bool _sessionPrepared;
    private bool _isLoading;
    private bool _isSubmitting;
    private string _statusMessage = "Chargement des plannings…";
    private Color _statusColor = Colors.Gold;
    private bool _isFormVisible;

    private DateTime _planningDay = DateTime.Today;
    private TimeSpan _startTime = new(9, 0, 0);
    private TimeSpan _endTime = new(17, 0, 0);

    public ReservationsViewModel()
    {
        RefreshCommand = new Command(async () => await LoadReservationsAsync());
        CreateCommand = new Command(async () => await CreateReservationAsync(), CanCreate);
        ToggleCreateFormCommand = new Command(() => IsFormVisible = !IsFormVisible);
    }

    public ObservableCollection<ReservationEntry> Reservations { get; } = new();

    public ICommand RefreshCommand { get; }

    public ICommand CreateCommand { get; }

    public ICommand ToggleCreateFormCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsSubmitting
    {
        get => _isSubmitting;
        set
        {
            if (SetProperty(ref _isSubmitting, value))
            {
                (CreateCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public bool IsFormVisible
    {
        get => _isFormVisible;
        set => SetProperty(ref _isFormVisible, value);
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

    public DateTime PlanningDay
    {
        get => _planningDay;
        set
        {
            if (SetProperty(ref _planningDay, value))
            {
                (CreateCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public TimeSpan StartTime
    {
        get => _startTime;
        set
        {
            if (SetProperty(ref _startTime, value))
            {
                (CreateCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public TimeSpan EndTime
    {
        get => _endTime;
        set
        {
            if (SetProperty(ref _endTime, value))
            {
                (CreateCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public async Task InitializeAsync()
    {
        if (!_sessionPrepared)
        {
            await PrepareSessionAsync();
        }

        await LoadReservationsAsync();
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
            Debug.WriteLine($"[RESERVATIONS] Session non préparée : {ex}");
            _sessionPrepared = true;
        }
    }

    private async Task LoadReservationsAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Chargement des plannings…";
            StatusColor = Colors.Gold;

            var items = await _apis.GetListAsync<ReservationEntry>("/api/crud/planning/list");

            Reservations.Clear();
            foreach (var item in items)
            {
                Reservations.Add(item);
            }

            StatusMessage = Reservations.Count == 0
                ? "Aucun planning pour le moment."
                : $"{Reservations.Count} planning(s) chargé(s).";
            StatusColor = Colors.LightGreen;
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "Chargement annulé.";
            StatusColor = Colors.Orange;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[RESERVATIONS] HTTP error: {ex}");
            StatusMessage = "Impossible de charger les plannings.";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RESERVATIONS] Erreur: {ex}");
            StatusMessage = "Erreur lors du chargement des plannings.";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanCreate()
    {
        return !IsSubmitting && EndTime > StartTime;
    }

    private async Task CreateReservationAsync()
    {
        if (IsSubmitting)
        {
            return;
        }

        if (EndTime <= StartTime)
        {
            StatusMessage = "L'heure de fin doit être supérieure à l'heure de début.";
            StatusColor = Colors.OrangeRed;
            return;
        }

        try
        {
            IsSubmitting = true;
            StatusMessage = "Création du planning…";
            StatusColor = Colors.Gold;

            if (!_sessionPrepared)
            {
                await PrepareSessionAsync();
            }

            var payload = new
            {
                jour = PlanningDay.ToString("yyyy-MM-dd"),
                heure_debut = StartTime.ToString(@"hh\:mm"),
                heure_fin = EndTime.ToString(@"hh\:mm")
            };

            var created = await _apis.PostBoolAsync("/api/crud/planning/create", payload);

            if (created)
            {
                StatusMessage = "Planning créé avec succès.";
                StatusColor = Colors.LightGreen;
                PlanningDay = DateTime.Today;
                StartTime = new TimeSpan(9, 0, 0);
                EndTime = new TimeSpan(17, 0, 0);
                IsFormVisible = false;
                await LoadReservationsAsync();
            }
            else
            {
                StatusMessage = "La création a échoué.";
                StatusColor = Colors.OrangeRed;
            }
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "Création annulée.";
            StatusColor = Colors.Orange;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[RESERVATIONS] HTTP error: {ex}");
            StatusMessage = "Impossible de créer le planning.";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RESERVATIONS] Erreur: {ex}");
            StatusMessage = "Une erreur est survenue lors de la création du planning.";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}

public class ReservationEntry
{
    public int Id { get; set; }
    public string? Jour { get; set; }
    public string? HeureDebut { get; set; }
    public string? HeureFin { get; set; }
    public DateTime? Date { get; set; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Jour) ? $"Planning #{Id}" : $"Planning {Jour}";
    public string DisplayDay => string.IsNullOrWhiteSpace(Jour)
        ? Date?.ToString("dddd dd MMMM") ?? "Jour non défini"
        : Jour;

    public string DisplayTimeRange
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(HeureDebut) && !string.IsNullOrWhiteSpace(HeureFin))
            {
                return $"{HeureDebut} - {HeureFin}";
            }

            return "Horaires non renseignés";
        }
    }
}
