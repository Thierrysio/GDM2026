using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System.Diagnostics;

namespace GDM2026;

[QueryProperty(nameof(Status), "status")]
public partial class ReservationsPage : ContentPage
{
    private readonly ReservationsViewModel _viewModel = new();

    public string? Status
    {
        get => _viewModel.Status;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _viewModel.Status = value;
                // S�lectionner le statut correspondant dans les tuiles
                _viewModel.SelectStatusByName(value);
            }
        }
    }

    public ReservationsPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.InitializeAsync();

            // Chargement automatique des réservations à chaque apparition de la page
            await _viewModel.ReloadWithFiltersAsync();
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"[RESERVATIONS PAGE] Erreur lors de l'initialisation : {ex}");
        }
    }
}
