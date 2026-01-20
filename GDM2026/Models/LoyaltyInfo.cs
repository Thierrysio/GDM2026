using Newtonsoft.Json;

namespace GDM2026.Models;

/// <summary>
/// Informations de fidélité d'un client récupérées après scan du QR code
/// </summary>
public class LoyaltyInfo
{
    [JsonProperty("userId")]
    public int UserId { get; set; }

    [JsonProperty("nom")]
    public string? Nom { get; set; }

    [JsonProperty("prenom")]
    public string? Prenom { get; set; }

    [JsonProperty("email")]
    public string? Email { get; set; }

    [JsonProperty("couronnes")]
    public int Couronnes { get; set; }

    /// <summary>
    /// Nom complet du client
    /// </summary>
    public string DisplayName => !string.IsNullOrWhiteSpace(Prenom) && !string.IsNullOrWhiteSpace(Nom)
        ? $"{Prenom} {Nom}"
        : Prenom ?? Nom ?? Email ?? $"Client #{UserId}";

    /// <summary>
    /// Valeur en euros des couronnes (10 couronnes = 0.10€)
    /// </summary>
    public decimal ValeurEnEuros => Couronnes * 0.01m;

    /// <summary>
    /// Affichage formaté des couronnes
    /// </summary>
    public string DisplayCouronnes => $"{Couronnes} couronne(s) = {ValeurEnEuros:C}";
}

/// <summary>
/// Réponse de l'API lors de la récupération des infos fidélité par QR code
/// </summary>
public class LoyaltyInfoResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("data")]
    public LoyaltyInfo? Data { get; set; }
}
