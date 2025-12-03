using System.Collections.Generic;

namespace GDM2026.Models;

public class OrderDetailsResponse
{
    public int Id { get; set; }

    public List<OrderLine> LesCommandes { get; set; } = new();
}

public class ChangeOrderLineStateRequest
{
    public int Id { get; set; }
}
