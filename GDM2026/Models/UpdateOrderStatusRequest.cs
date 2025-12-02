using Newtonsoft.Json;

namespace GDM2026.Models;

public class UpdateOrderStatusRequest
{
    [JsonProperty("commandeId")]
    public int CommandeId { get; set; }

    [JsonProperty("etat")]
    public string? Etat { get; set; }
}
