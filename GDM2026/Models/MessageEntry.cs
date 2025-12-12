using System;
using Newtonsoft.Json;

namespace GDM2026.Models;

public class MessageEntry
{
    [JsonProperty("id")]
    public int Id { get; set; }

    // ✅ Compat API (camelCase)
    [JsonProperty("dateMessage")]
    public DateTimeOffset? DateMessage { get; set; }

    // ✅ Optionnel : compat ancienne API (snake_case) si tu en as
    // (Newtonsoft ne peut pas mapper 2 JsonProperty sur la même prop,
    //  donc on ajoute une propriété proxy)
    [JsonProperty("date_message")]
    private DateTimeOffset? DateMessageSnake
    {
        set
        {
            // Si dateMessage n'est pas présent mais date_message l'est
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

    // Optionnel, mais utile si tu l’affiches plus tard
    [JsonProperty("leUser")]
    public int? LeUser { get; set; }

    public string DisplayDate =>
        DateMessage?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Date inconnue";
}
