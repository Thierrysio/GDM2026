using GDM2026.ViewModels;
using Microsoft.Maui.Controls;

namespace GDM2026;

public partial class CommentsPage : ContentPage
{
    private readonly CommentsViewModel _viewModel = new();

    public CommentsPage()
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
