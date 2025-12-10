using Newtonsoft.Json;

namespace GDM2026.Models;

public class SubCategory
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("nom")]
    public string? Name { get; set; }

    [JsonProperty("lacategorieParent")]
    public int? ParentCategoryId { get; set; }

    [JsonIgnore]
    public string? ParentCategoryName { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Sous-catégorie #{Id}" : Name!;

    public string ParentDisplayName => ParentCategoryId.HasValue
        ? string.IsNullOrWhiteSpace(ParentCategoryName)
            ? $"Catégorie parente #{ParentCategoryId}"
            : ParentCategoryName!
        : "Aucune catégorie parente";
}
