namespace GDM2026.Models
{
    public class UsePointsRequest
    {
        public int LoyaltyUserId { get; set; }
        public decimal AmountInEuros { get; set; }
        public int CouronnesUsed { get; set; }
    }
}
