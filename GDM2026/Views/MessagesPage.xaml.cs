using GDM2026.ViewModels;
using Microsoft.Maui.Controls;

namespace GDM2026.Views;

public partial class MessagesPage : ContentPage
{
    private readonly MessagesViewModel _viewModel = new();

    public MessagesPage()
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
