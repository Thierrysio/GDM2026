using Newtonsoft.Json;

namespace GDM2026.Models;

public class OrderStatusNotificationRequest
{
    [JsonProperty("orderId")]
    public int OrderId { get; set; }

    [JsonProperty("userId")]
    public int UserId { get; set; }

    [JsonProperty("channel")]
    public string Channel { get; set; } = "notification";
}
