namespace GDM2026.Models;

public class AdminImage
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ImageName { get; set; }

    public string FullUrl => string.IsNullOrWhiteSpace(Url)
        ? string.Empty
        : $"{Constantes.BaseImagesAddress}{Url}";

    public string DisplayName => string.IsNullOrWhiteSpace(ImageName)
        ? System.IO.Path.GetFileName(Url)
        : ImageName;
}
