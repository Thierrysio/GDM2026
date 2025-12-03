using GDM2026.ViewModels;

namespace GDM2026;

[QueryProperty(nameof(Status), "status")]
public partial class OrderStatusPage : ContentPage
{
    private readonly OrderStatusPageViewModel _viewModel = new();

    public string? Status
    {
        get => _viewModel.Status;
        set => _viewModel.Status = value;
    }

    public OrderStatusPage()
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
