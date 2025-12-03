using GDM2026.ViewModels;

namespace GDM2026
{
    public partial class HomePage : ContentPage
    {
        private readonly HomePageViewModel _viewModel = new();

        public HomePage()
        {
            InitializeComponent();
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.OnDisappearing();
        }
    }
}
