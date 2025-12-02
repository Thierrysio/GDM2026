using Newtonsoft.Json;

namespace GDM2026.Models;

public class OrderStatusRequest
{
    [JsonProperty("cd")]
    public string? Cd { get; set; }
}
