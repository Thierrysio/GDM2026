using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System;

namespace GDM2026;

public partial class ProductsEditPage : ContentPage
{
    private readonly ProductsEditViewModel _viewModel = new();

    public ProductsEditPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception)
        {
            // Already logged in viewmodel
        }
    }
}
