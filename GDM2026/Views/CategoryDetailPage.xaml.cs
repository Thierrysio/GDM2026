using GDM2026.Models;
using GDM2026.ViewModels;

namespace GDM2026
{
    [QueryProperty(nameof(Card), "card")]
    public partial class CategoryDetailPage : ContentPage
    {
        private readonly CategoryDetailViewModel _viewModel = new();

        public CategoryCard? Card
        {
            get => _viewModel.Card;
            set => _viewModel.Card = value;
        }

        public CategoryDetailPage()
        {
            InitializeComponent();
            BindingContext = _viewModel;
        }
    }
}
