using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System.Diagnostics;

namespace GDM2026;

public partial class PartnersPage : ContentPage
{
    private readonly PartnersViewModel _viewModel = new();

    public PartnersPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // ✅ Comme Evenement : ne charge rien au démarrage
            await _viewModel.OnPageAppearingAsync();
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"[PARTNERS PAGE] Erreur lors de l'initialisation : {ex}");
        }
    }
}
