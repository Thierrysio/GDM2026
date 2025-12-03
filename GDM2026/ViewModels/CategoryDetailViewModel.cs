using GDM2026.Models;

namespace GDM2026.ViewModels;

public class CategoryDetailViewModel : BaseViewModel
{
    private CategoryCard? _card;
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _hint = string.Empty;

    public CategoryCard? Card
    {
        get => _card;
        set
        {
            if (SetProperty(ref _card, value))
            {
                ApplyCard(value);
            }
        }
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string Hint
    {
        get => _hint;
        set => SetProperty(ref _hint, value);
    }

    public void ApplyCard(CategoryCard? card)
    {
        if (card == null)
        {
            Title = "Catégorie introuvable";
            Description = "Impossible de charger les détails de cette catégorie.";
            Hint = "Retournez à l'accueil et sélectionnez une catégorie.";
            return;
        }

        Title = card.Title;
        Description = card.Description;
        Hint = $"Vous êtes sur la page {card.Title}. Ajoutez ici les fonctionnalités spécifiques à cette catégorie.";
    }
}
