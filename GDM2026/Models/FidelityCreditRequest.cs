using Newtonsoft.Json;

namespace GDM2026.Models;

public class FidelityCreditRequest
{
    [JsonProperty("userId")]
    public int UserId { get; set; }

    [JsonProperty("commandeId")]
    public int CommandeId { get; set; }

    [JsonProperty("pointsToAdd")]
    public int PointsToAdd { get; set; }
}
