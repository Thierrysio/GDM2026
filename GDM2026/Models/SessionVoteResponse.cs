using Newtonsoft.Json;

namespace GDM2026.Models;

public class SessionVoteResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("sessionVoteId")]
    public int? SessionVoteId { get; set; }

    [JsonProperty("produitCandidatId")]
    public int? ProduitCandidatId { get; set; }
}
