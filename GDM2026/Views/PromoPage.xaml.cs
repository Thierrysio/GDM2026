using System.Diagnostics;
using GDM2026.ViewModels;
using Microsoft.Maui.ApplicationModel;
using PreserveAttribute = Microsoft.Maui.Controls.Internals.PreserveAttribute;

namespace GDM2026.Views;

[Preserve(AllMembers = true)]
public partial class PromoPage : ContentPage
{
    private readonly PromoPageViewModel _viewModel = new();

    public PromoPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
        _viewModel.PromoSaved += OnPromoSaved;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PROMO_PAGE] OnAppearing crash: {ex}");
            await DisplayAlertAsync("Erreur", "Impossible de charger la page promotion.", "OK");
        }
    }

    private void OnPromoSaved(object? sender, string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await DisplayAlertAsync("Succès", message, "OK");
        });
    }
}
