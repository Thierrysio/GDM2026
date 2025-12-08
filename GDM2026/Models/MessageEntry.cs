using System;
using Newtonsoft.Json;

namespace GDM2026.Models;

public class MessageEntry
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("date_message")]
    public DateTime? DateMessage { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("reponse")]
    public string? Reponse { get; set; }

    [JsonProperty("etat")]
    public string? Etat { get; set; }

    public string DisplayDate => DateMessage?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Date inconnue";
}
