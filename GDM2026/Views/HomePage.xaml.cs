using GDM2026.ViewModels;
using Microsoft.Maui.Controls;

namespace GDM2026;

public partial class HomePage : ContentPage
{
    private readonly HomePageViewModel _viewModel = new();

    public HomePage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _ = _viewModel.InitializeAsync();
    }
}
