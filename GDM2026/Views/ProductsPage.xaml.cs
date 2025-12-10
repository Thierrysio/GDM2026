using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;

namespace GDM2026;

public partial class ProductsPage : ContentPage
{
    private readonly ProductsViewModel _viewModel = new();

    public ProductsPage()
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[PRODUCTS PAGE] Erreur lors de l'initialisation : {ex}");
        }
    }

    private async void OnEditProductClicked(object sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync(nameof(ProductsEditPage), animate: false);
        }
    }
}
