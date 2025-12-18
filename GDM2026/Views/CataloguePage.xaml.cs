using GDM2026.ViewModels;

namespace GDM2026.Views;

public partial class CataloguePage : ContentPage
{
    private readonly CatalogueViewModel _viewModel = new();

    public CataloguePage()
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
