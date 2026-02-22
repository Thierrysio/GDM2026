using Newtonsoft.Json;

namespace GDM2026.Models;

public class VoteResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("moyenneNotes")]
    public double? MoyenneNotes { get; set; }

    [JsonProperty("nouvelleNoteMoyenne")]
    public double? NouvelleNoteMoyenne { get; set; }

    [JsonProperty("nombreVotes")]
    public int? NombreVotes { get; set; }

    [JsonProperty("nouveauNombreVotes")]
    public int? NouveauNombreVotes { get; set; }

    public double DisplayMoyenne => MoyenneNotes ?? NouvelleNoteMoyenne ?? 0;

    public int DisplayNombreVotes => NombreVotes ?? NouveauNombreVotes ?? 0;
}
