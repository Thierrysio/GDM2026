using Newtonsoft.Json;

namespace GDM2026.Models;

public class CreateSessionVoteRequest
{
    [JsonProperty("titre")]
    public string? Titre { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("dateDebut")]
    public string? DateDebut { get; set; }

    [JsonProperty("dateFin")]
    public string? DateFin { get; set; }
}
