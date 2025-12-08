using System;
using Newtonsoft.Json;

namespace GDM2026.Models;

public class Actualite
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("titre")]
    public string Titre { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("image")]
    public string? Image { get; set; }

    [JsonProperty("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTime? UpdatedAt { get; set; }

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
