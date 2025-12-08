using Newtonsoft.Json;

namespace GDM2026.Models;

public class SubCategory
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("nom")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("categorieParent")]
    public int? ParentCategoryId { get; set; }

    [JsonProperty("categorieParentNom")]
    public string? ParentCategoryName { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Sous-catégorie #{Id}" : Name!;

    public string ParentDisplayName => ParentCategoryId.HasValue
        ? string.IsNullOrWhiteSpace(ParentCategoryName)
            ? $"Catégorie parente #{ParentCategoryId}"
            : ParentCategoryName!
        : "Aucune catégorie parente";
}
