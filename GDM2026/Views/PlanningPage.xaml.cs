using GDM2026.ViewModels;

namespace GDM2026
{
    public partial class PlanningPage : ContentPage
    {
        private PlanningViewModel ViewModel => (PlanningViewModel)BindingContext;

        public PlanningPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await ViewModel.OnPageAppearingAsync();
        }
    }
}
