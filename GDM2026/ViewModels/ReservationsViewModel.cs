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
    private string _statusMessage = "Chargement des réservations…";
    private Color _statusColor = Colors.Gold;
    private bool _isFormVisible;

    private string _customerName = string.Empty;
    private DateTime _reservationDate = DateTime.Today;

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

    public string CustomerName
    {
        get => _customerName;
        set
        {
            if (SetProperty(ref _customerName, value))
            {
                (CreateCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public DateTime ReservationDate
    {
        get => _reservationDate;
        set => SetProperty(ref _reservationDate, value);
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
            StatusMessage = "Chargement des réservations…";
            StatusColor = Colors.Gold;

            var items = await _apis.GetListAsync<ReservationEntry>("/api/crud/reservations/list");

            Reservations.Clear();
            foreach (var item in items)
            {
                Reservations.Add(item);
            }

            StatusMessage = Reservations.Count == 0
                ? "Aucune réservation pour le moment."
                : $"{Reservations.Count} réservation(s) chargée(s).";
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
            StatusMessage = "Impossible de charger les réservations.";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RESERVATIONS] Erreur: {ex}");
            StatusMessage = "Erreur lors du chargement des réservations.";
            StatusColor = Colors.OrangeRed;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanCreate()
    {
        return !IsSubmitting && !string.IsNullOrWhiteSpace(CustomerName);
    }

    private async Task CreateReservationAsync()
    {
        if (IsSubmitting)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            StatusMessage = "Renseignez le nom du client.";
            StatusColor = Colors.OrangeRed;
            return;
        }

        try
        {
            IsSubmitting = true;
            StatusMessage = "Création de la réservation…";
            StatusColor = Colors.Gold;

            if (!_sessionPrepared)
            {
                await PrepareSessionAsync();
            }

            var payload = new
            {
                nom_client = CustomerName.Trim(),
                date = ReservationDate.ToString("yyyy-MM-dd")
            };

            var created = await _apis.PostBoolAsync("/api/crud/reservations/create", payload);

            if (created)
            {
                StatusMessage = "Réservation créée avec succès.";
                StatusColor = Colors.LightGreen;
                CustomerName = string.Empty;
                ReservationDate = DateTime.Today;
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
            StatusMessage = "Impossible de créer la réservation.";
            StatusColor = Colors.OrangeRed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RESERVATIONS] Erreur: {ex}");
            StatusMessage = "Une erreur est survenue lors de la création.";
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
    public string? NomClient { get; set; }
    public DateTime? Date { get; set; }
    public string DisplayTitle => $"Réservation #{Id}";
    public string DisplayClient => string.IsNullOrWhiteSpace(NomClient) ? "Client inconnu" : NomClient!;
    public string DisplayDate => Date?.ToString("dd/MM/yyyy") ?? "Date non renseignée";
}
