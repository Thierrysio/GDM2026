using System;
using Newtonsoft.Json;

namespace GDM2026.Models;

public class MessageEntry
{
    [JsonProperty("id")]
    public int Id { get; set; }

    // ✅ correspond à ton JSON : "dateMessage"
    [JsonProperty("dateMessage")]
    public DateTime? DateMessage { get; set; }

    // (optionnel) compat ancienne clé
    [JsonProperty("date_message")]
    private DateTime? DateMessageSnake
    {
        set
        {
            if (DateMessage is null)
                DateMessage = value;
        }
    }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("reponse")]
    public string? Reponse { get; set; }

    [JsonProperty("etat")]
    public string? Etat { get; set; }

    [JsonProperty("leUser")]
    public int? LeUser { get; set; }

    public string DisplayDate => DateMessage?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Date inconnue";
}
