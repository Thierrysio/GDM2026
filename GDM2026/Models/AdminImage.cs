using Microsoft.Maui.Controls;
using Newtonsoft.Json;
using System;

namespace GDM2026.Models;

public class AdminImage
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("categorie")]
    public string? Category { get; set; }

    [JsonProperty("contentType")]
    public string? ContentType { get; set; }

    [JsonIgnore]
    public Uri? ImageUri { get; set; }

    [JsonIgnore]
    public ImageSource? Thumbnail { get; set; }

    [JsonIgnore]
    public string? DisplayName { get; set; }
}
