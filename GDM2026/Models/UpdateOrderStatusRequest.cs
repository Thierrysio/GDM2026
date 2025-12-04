using Newtonsoft.Json;

namespace GDM2026.Models;

public class UpdateOrderStatusRequest
{
    [JsonProperty("Id")]
    public int Id { get; set; }

    [JsonProperty("Etat")]
    public string? Etat { get; set; }
}
