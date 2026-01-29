using System;

namespace GDM2026.Models;

public class AdminImage
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ImageName { get; set; }

    public string FullUrl => BuildFullUrl(Url);

    public string DisplayName => string.IsNullOrWhiteSpace(ImageName)
        ? System.IO.Path.GetFileName(Url)
        : ImageName;

    private static string BuildFullUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var sanitized = path.Replace("\\", "/").Trim();

        if (Uri.TryCreate(sanitized, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        var trimmedPath = sanitized.TrimStart('/');
        if (!trimmedPath.Contains('/') && !trimmedPath.Contains('\\'))
        {
            trimmedPath = $"images/{trimmedPath}";
        }
        var baseAddress = Constantes.BaseImagesAddress.TrimEnd('/');
        return $"{baseAddress}/{trimmedPath}";
    }
}
