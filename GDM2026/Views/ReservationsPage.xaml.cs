using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System.Diagnostics;

namespace GDM2026;

public partial class ReservationsPage : ContentPage
{
    private readonly ReservationsViewModel _viewModel = new();

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
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"[RESERVATIONS PAGE] Erreur lors de l'initialisation : {ex}");
        }
    }
}
