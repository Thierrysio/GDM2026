namespace GDM2026.Models;

public class Histoire
{
    public int Id { get; set; }

    public string? Titre { get; set; }

    public string? Texte { get; set; }

    public string? UrlImage { get; set; }

    public DateTime? DateHistoire { get; set; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Titre) ? "(Sans titre)" : Titre;

    public string DisplayDate => DateHistoire?.ToString("dd/MM/yyyy") ?? "Date inconnue";

    public string DisplayImage => string.IsNullOrWhiteSpace(UrlImage) ? "—" : UrlImage;
}
