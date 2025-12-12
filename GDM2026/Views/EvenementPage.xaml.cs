using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System;

namespace GDM2026.Views
{
    public partial class EvenementPage : ContentPage
    {
        private readonly EvenementPageViewModel _viewModel = new();
        private bool _initialized;

        public EvenementPage()
        {
            InitializeComponent();
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_initialized)
                return;

            _initialized = true;

            try
            {
                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EVENEMENT PAGE] Init error: {ex}");
            }
        }
    }
}
