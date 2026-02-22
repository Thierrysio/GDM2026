using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace GDM2026.Models;

public class SessionVote
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("titre")]
    public string? Titre { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("dateDebut")]
    public DateTime? DateDebut { get; set; }

    [JsonProperty("dateFin")]
    public DateTime? DateFin { get; set; }

    [JsonProperty("statut")]
    public string? Statut { get; set; }

    [JsonProperty("produitsCandidat")]
    public List<ProduitCandidat>? ProduitsCandidat { get; set; }

    public bool IsActive => string.Equals(Statut, "active", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(Statut, "EnCours", StringComparison.OrdinalIgnoreCase);

    public bool IsTerminee => string.Equals(Statut, "terminee", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(Statut, "Terminee", StringComparison.OrdinalIgnoreCase);

    public string StatutLabel => Statut switch
    {
        "active" or "EnCours" => "En cours",
        "terminee" or "Terminee" => "Terminee",
        "brouillon" or "Annulee" => "Brouillon",
        _ => Statut ?? "Inconnu"
    };

    public string PeriodeLabel
    {
        get
        {
            if (DateDebut == null || DateFin == null)
                return "Dates non definies";

            return $"Du {DateDebut:dd/MM/yyyy} au {DateFin:dd/MM/yyyy}";
        }
    }
}
