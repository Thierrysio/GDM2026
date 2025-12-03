using GDM2026.Models;
using GDM2026.ViewModels;
using Microsoft.Maui.Controls;
using System.Collections.Generic;

namespace GDM2026
{
    public partial class CategoryDetailPage : ContentPage, IQueryAttributable
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

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("card", out var card) && card is CategoryCard selectedCard)
            {
                Card = selectedCard;
                return;
            }

            _viewModel.ApplyCard(null);
        }
    }
}
