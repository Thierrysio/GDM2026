using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;

namespace GDM2026;

public partial class UsersPage : ContentPage
{
    private readonly UsersViewModel _viewModel = new();

    public UsersPage()
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
            Debug.WriteLine($"[USERS PAGE] Erreur lors de l'initialisation : {ex}");
        }
    }
}
