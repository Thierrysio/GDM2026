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
            await _viewModel.InitializeAsync();
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"[PARTNERS PAGE] Erreur lors de l'initialisation : {ex}");
            // Optionnel : informer l'utilisateur via un affichage ou en mettant StatusMessage dans le VM
            // _viewModel.StatusMessage = "Impossible de charger les partenaires.";
        }
    }
}
