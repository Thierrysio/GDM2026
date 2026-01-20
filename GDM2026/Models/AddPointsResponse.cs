namespace GDM2026.Models
{
    public class AddPointsResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int NewCouronnesBalance { get; set; }
        public decimal NewEuroValue { get; set; }
        public int PointsAdded { get; set; }
    }
}
