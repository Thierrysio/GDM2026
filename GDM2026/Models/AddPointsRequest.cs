namespace GDM2026.Models
{
    public class AddPointsRequest
    {
        public int LoyaltyUserId { get; set; }
        public decimal AmountInEuros { get; set; }
        public int PointsToAdd { get; set; }
    }
}
