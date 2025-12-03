using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GDM2026.Models;

public class OrderDetailsResponse
{
    public int Id { get; set; }

    public DateTime DateCommande { get; set; }

    public double MontantTotal { get; set; }

    public bool Valider { get; set; }

    public string? Etat { get; set; }

    [JsonProperty("produits")]
    public List<OrderLine> LesCommandes { get; set; } = new();
}

public class OrderDetailsRequest
{
    public int Id { get; set; }
}

public class ChangeOrderLineStateRequest
{
    public int Id { get; set; }
}
