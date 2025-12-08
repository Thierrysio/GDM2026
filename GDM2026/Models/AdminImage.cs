using System;

namespace GDM2026.Models;

public class AdminImage
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ImageName { get; set; }

    public string FullUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                return string.Empty;
            }

            if (Uri.TryCreate(Url, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            var relativePath = Url.StartsWith("/") ? Url : $"/{Url}";
            return $"{Constantes.BaseImagesAddress}{relativePath}";
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(ImageName)
        ? System.IO.Path.GetFileName(Url)
        : ImageName;
}
