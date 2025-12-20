using Newtonsoft.Json;

namespace GDM2026.Models;

public class UpdateLineQuantityRequest
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("quantite")]
    public int Quantite { get; set; }
}
