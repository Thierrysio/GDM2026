using Newtonsoft.Json;

namespace GDM2026.Models;

public class UpdateStockRequest
{
    [JsonProperty("produitId")]
    public int ProduitId { get; set; }

    [JsonProperty("quantite")]
    public int Quantite { get; set; }
}
