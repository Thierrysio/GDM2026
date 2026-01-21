using Newtonsoft.Json;

namespace GDM2026.Models;

/// <summary>
/// Requête pour appliquer les points fidélité sur une commande
/// </summary>
public class ApplyLoyaltyRequest
{
    /// <summary>
    /// ID de la commande/réservation
    /// </summary>
    [JsonProperty("commandeId")]
    public int CommandeId { get; set; }

    /// <summary>
    /// ID de l'utilisateur client
    /// </summary>
    [JsonProperty("userId")]
    public int UserId { get; set; }

    /// <summary>
    /// Nombre de couronnes à utiliser
    /// </summary>
    [JsonProperty("couronnesUtilisees")]
    public int CouronnesUtilisees { get; set; }

    /// <summary>
    /// Montant de la réduction en euros (calculé : couronnes / 15)
    /// </summary>
    [JsonProperty("montantReduction")]
    public double MontantReduction { get; set; }
}

/// <summary>
/// Réponse de l'API après application des points fidélité
/// </summary>
public class ApplyLoyaltyResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    /// <summary>
    /// ID de l'utilisateur pour lequel la réduction a été appliquée.
    /// Utilisé pour propager l'information côté client lorsque l'API ne la renvoie pas.
    /// </summary>
    [JsonProperty("userId")]
    public int UserId { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Nouveau solde de couronnes du client après déduction
    /// </summary>
    [JsonProperty("nouveauSoldeCouronnes")]
    public int NouveauSoldeCouronnes { get; set; }

    /// <summary>
    /// Nouveau montant total de la commande après réduction
    /// </summary>
    [JsonProperty("nouveauMontantCommande")]
    public double NouveauMontantCommande { get; set; }

    /// <summary>
    /// Montant de la réduction appliquée
    /// </summary>
    [JsonProperty("reductionAppliquee")]
    public double ReductionAppliquee { get; set; }
}

/// <summary>
/// Requête pour récupérer les infos fidélité d'un client par son QR code
/// </summary>
public class GetLoyaltyByQrCodeRequest
{
    /// <summary>
    /// Contenu du QR code scanné (généralement l'ID utilisateur ou un token)
    /// </summary>
    [JsonProperty("qrCode")]
    public string QrCode { get; set; } = string.Empty;
}
