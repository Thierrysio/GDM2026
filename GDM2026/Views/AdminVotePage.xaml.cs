using GDM2026.ViewModels;
using Microsoft.Maui.Controls;

namespace GDM2026;

public partial class AdminVotePage : ContentPage
{
    private readonly AdminVoteViewModel _viewModel = new();
    private bool _initialized;

    public AdminVotePage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_initialized)
        {
            _initialized = true;
            await _viewModel.InitializeAsync();
        }
    }
}
