using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;

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

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[COMMENTS_PAGE] Crash OnAppearing: {ex}");
            // Evite un crash silencieux : on affiche un message
            await DisplayAlert("Erreur", "Impossible de charger les commentaires.", "OK");
        }
    }
}
