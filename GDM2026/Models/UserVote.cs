using Newtonsoft.Json;
using System;

namespace GDM2026.Models;

public class UserVote
{
    [JsonProperty("produitCandidatId")]
    public int ProduitCandidatId { get; set; }

    [JsonProperty("note")]
    public double Note { get; set; }

    [JsonProperty("dateVote")]
    public DateTime? DateVote { get; set; }

    public string NoteLabel => $"{Note:F0}/5";

    public string DateLabel => DateVote?.ToString("dd/MM/yyyy HH:mm") ?? "Date inconnue";
}
