using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace GDM2026.Models;

public class ReservationOrder
{
    public int Id { get; set; }

    public string? Etat { get; set; }

    public bool Valider { get; set; }

    public double MontantTotal { get; set; }

    public string? DateCommande { get; set; }

    public List<ReservationSlot> Reservations { get; set; } = new();

    [JsonProperty("lignesCommande")]
    public List<ReservationOrderLine> LignesCommande { get; set; } = new();
}

public class ReservationSlot
{
    public int Id { get; set; }

    public string? Date { get; set; }

    public string? Heure { get; set; }

    public string? Planning { get; set; }
}

public class ReservationOrderLine
{
    public int Id { get; set; }

    public int Quantite { get; set; }

    [JsonProperty("prixretenu")]
    public double PrixRetenu { get; set; }

    public string? Produit { get; set; }
}

public class ReservationStatusRequest
{
    public string? Etat { get; set; }

    public string? DateDebut { get; set; }

    public string? DateFin { get; set; }
}
