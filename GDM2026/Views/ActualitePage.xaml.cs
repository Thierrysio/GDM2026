using GDM2026.ViewModels;
using Microsoft.Maui.Controls;

namespace GDM2026;

public partial class ActualitePage : ContentPage
{
    private readonly ActualiteViewModel _viewModel = new();

    public ActualitePage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.EnsureActualitesLoadedAsync();
    }
}
