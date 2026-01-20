using Newtonsoft.Json;

namespace GDM2026.Models
{
    public class UsePointsRequest
    {
        [JsonProperty("LoyaltyUserId")]
        public int LoyaltyUserId { get; set; }

        [JsonProperty("AmountInEuros")]
        public decimal AmountInEuros { get; set; }

        [JsonProperty("CouronnesUsed")]
        public int CouronnesUsed { get; set; }
    }
}
