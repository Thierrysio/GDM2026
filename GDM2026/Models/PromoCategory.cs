using Newtonsoft.Json;
using PreserveAttribute = Microsoft.Maui.Controls.Internals.PreserveAttribute;

namespace GDM2026.Models;

[Preserve(AllMembers = true)]
public class PromoCategory
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("nom")]
    public string? Name { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? $"Catégorie promo #{Id}"
        : Name!;
}
