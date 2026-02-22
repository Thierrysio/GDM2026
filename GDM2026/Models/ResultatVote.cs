using Newtonsoft.Json;

namespace GDM2026.Models;

public class ResultatVote
{
    [JsonProperty("produitCandidatId")]
    public int ProduitCandidatId { get; set; }

    [JsonProperty("nomProduit")]
    public string? NomProduit { get; set; }

    [JsonProperty("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonProperty("moyenneNotes")]
    public double MoyenneNotes { get; set; }

    [JsonProperty("nombreVotes")]
    public int NombreVotes { get; set; }

    [JsonProperty("classement")]
    public int Classement { get; set; }

    public string FullImageUrl => BuildFullUrl(ImageUrl);

    public string ClassementLabel => Classement switch
    {
        1 => "1er",
        _ => $"{Classement}e"
    };

    public string MoyenneLabel => $"{MoyenneNotes:F1}/5";

    public string VotesLabel => NombreVotes == 1 ? "1 vote" : $"{NombreVotes} votes";

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

        var relative = sanitized.TrimStart('/');
        if (!relative.Contains('/'))
        {
            relative = $"images/{relative}";
        }

        var baseAddress = Constantes.BaseImagesAddress?.TrimEnd('/') ?? string.Empty;
        return string.IsNullOrWhiteSpace(baseAddress)
            ? relative
            : $"{baseAddress}/{relative}";
    }
}
