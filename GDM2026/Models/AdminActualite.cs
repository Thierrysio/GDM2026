using System;

namespace GDM2026.Models;

public class AdminActualite
{
    public int Id { get; set; }
    public string? Titre { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
    public DateTime? CreatedAt { get; set; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(Titre) ? "(Sans titre)" : Titre;

    public string DisplayDescription => string.IsNullOrWhiteSpace(Description)
        ? "Aucune description fournie."
        : Description;

    public string FullImageUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Image))
            {
                return string.Empty;
            }

            if (Uri.TryCreate(Image, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            var relativePath = Image.StartsWith("/") ? Image : $"/{Image}";
            return $"{Constantes.BaseImagesAddress}{relativePath}";
        }
    }
}
