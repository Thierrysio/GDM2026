using Newtonsoft.Json;

namespace GDM2026.Models;

public class UpdateOrderStatusRequest
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("etat")]
    public string? Etat { get; set; }
}
