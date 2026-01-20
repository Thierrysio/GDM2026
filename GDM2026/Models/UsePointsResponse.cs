using Newtonsoft.Json;

namespace GDM2026.Models
{
    public class UsePointsResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        // Nouveau solde de couronnes côté serveur
        [JsonProperty("nouveauSoldeCouronnes")]
        public int NouveauSoldeCouronnes { get; set; }

        // Montant déduit par les points
        [JsonProperty("montantDeduit")]
        public decimal MontantDeduit { get; set; }

        // Montant restant éventuel
        [JsonProperty("montantRestant")]
        public decimal MontantRestant { get; set; }
    }
}
