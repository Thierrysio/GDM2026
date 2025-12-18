using System;
using System.Globalization;
using Newtonsoft.Json;

namespace GDM2026.Models;

public class Planning
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("jour")]
    public DateTime? Jour { get; set; }

    [JsonProperty("heureDebut")]
    public string? HeureDebut { get; set; }

    [JsonProperty("heureFin")]
    public string? HeureFin { get; set; }

    [JsonIgnore]
    public TimeSpan? HeureDebutSpan => ParseTime(HeureDebut);

    [JsonIgnore]
    public TimeSpan? HeureFinSpan => ParseTime(HeureFin);

    [JsonIgnore]
    public string DisplayDate => Jour?.ToString("dddd dd MMMM yyyy", new CultureInfo("fr-FR")) ?? "Date non définie";

    [JsonIgnore]
    public string DisplayHours
    {
        get
        {
            var debut = HeureDebutSpan?.ToString(@"hh\:mm") ?? "--:--";
            var fin = HeureFinSpan?.ToString(@"hh\:mm") ?? "--:--";
            return $"{debut} → {fin}";
        }
    }

    private static TimeSpan? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TimeSpan.TryParse(value, out var span) ? span : null;
    }
}
