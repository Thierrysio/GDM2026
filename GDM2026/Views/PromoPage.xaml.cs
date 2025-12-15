using GDM2026.ViewModels;

namespace GDM2026.Views;

public partial class PromoPage : ContentPage
{
    private readonly PromoPageViewModel _viewModel = new();

    public PromoPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
