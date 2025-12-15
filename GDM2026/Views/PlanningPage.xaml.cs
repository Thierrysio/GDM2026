using System.Diagnostics;
using GDM2026.ViewModels;

namespace GDM2026
{
    public partial class PlanningPage : ContentPage
    {
        private readonly PlanningViewModel _viewModel = new();

        public PlanningPage()
        {
            InitializeComponent();
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                await _viewModel.OnPageAppearingAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PLANNING] OnAppearing crash: {ex}");
                await DisplayAlert("Erreur", "Impossible de charger le planning.", "OK");
            }
        }
    }
}
