using GDM2026.ViewModels;

namespace GDM2026.Views;

public partial class HistoirePage : ContentPage
{
    private readonly HistoireViewModel _viewModel = new();

    public HistoirePage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnPageAppearingAsync();
    }
}
