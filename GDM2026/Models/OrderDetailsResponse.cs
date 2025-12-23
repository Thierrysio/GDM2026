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

    public int? UserId { get; set; }

    [JsonProperty("user_id")]
    private int? UserIdSnakeAlias { set => UserId = value; get => UserId; }

    public int? UserIdFidelite { get; set; }

    [JsonProperty("userIdFidelite")]
    private int? UserIdFideliteCamelAlias { set => UserIdFidelite = value; get => UserIdFidelite; }

    [JsonProperty("user_id_fidelite")]
    private int? UserIdFideliteSnakeAlias { set => UserIdFidelite = value; get => UserIdFidelite; }

    [JsonProperty("produits")]
    public List<OrderLine> LesCommandes { get; set; } = new();
}

public class OrderDetailsRequest
{
    public int Id { get; set; }
}

public class ChangeOrderLineStateRequest
{
    [JsonProperty("Id")]
    public int Id { get; set; }

    [JsonProperty("Etat")]
    public string Etat { get; set; } = string.Empty;
}

public class ChangeOrderStateRequest
{
    [JsonProperty("Id")]
    public int Id { get; set; }

    [JsonProperty("Etat")]
    public string Etat { get; set; } = string.Empty;
}
