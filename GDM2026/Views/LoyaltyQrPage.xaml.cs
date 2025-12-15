using GDM2026.ViewModels;

namespace GDM2026.Views
{
    public partial class LoyaltyQrPage : ContentPage
    {
        private readonly LoyaltyQrViewModel _viewModel = new();

        public LoyaltyQrPage()
        {
            InitializeComponent();
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }

        private async void OnCloseClicked(object sender, EventArgs e)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("..", animate: true);
            }
        }
    }
}
