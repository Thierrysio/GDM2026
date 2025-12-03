using GDM2026.ViewModels;

namespace GDM2026
{
    public partial class MainPage : ContentPage
    {
        private readonly MainPageViewModel _viewModel = new();

        public MainPage()
        {
            InitializeComponent();
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await _viewModel.InitializeAsync();
        }
    }
}
