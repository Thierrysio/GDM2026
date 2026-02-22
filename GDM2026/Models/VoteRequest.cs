using Newtonsoft.Json;

namespace GDM2026.Models;

public class VoteRequest
{
    [JsonProperty("userId")]
    public int UserId { get; set; }

    [JsonProperty("produitCandidatId")]
    public int ProduitCandidatId { get; set; }

    [JsonProperty("sessionVoteId")]
    public int SessionVoteId { get; set; }

    [JsonProperty("note")]
    public double Note { get; set; }
}
