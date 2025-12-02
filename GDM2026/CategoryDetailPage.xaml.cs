using Microsoft.Maui.Controls;

namespace GDM2026
{
    [QueryProperty(nameof(Card), "card")]
    public partial class CategoryDetailPage : ContentPage
    {
        private CategoryCard? _card;

        public CategoryCard? Card
        {
            get => _card;
            set
            {
                _card = value;
                UpdateContent();
            }
        }

        public CategoryDetailPage()
        {
            InitializeComponent();
        }

        private void UpdateContent()
        {
            if (_card == null)
            {
                return;
            }

            Title = _card.Title;
            TitleLabel.Text = _card.Title;
            DescriptionLabel.Text = _card.Description;
            HintLabel.Text = $"Vous êtes sur la page {_card.Title}. Ajoutez ici les fonctionnalités spécifiques à cette catégorie.";
        }
    }
}
