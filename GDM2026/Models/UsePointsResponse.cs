namespace GDM2026.Models
{
    public class UsePointsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int NewCouronnesBalance { get; set; }
        public decimal NewEuroValue { get; set; }
    }
}
