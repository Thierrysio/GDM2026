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
            await _viewModel.OnPageAppearingAsync();
        }
    }
}
