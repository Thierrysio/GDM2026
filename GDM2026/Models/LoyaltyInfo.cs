using Newtonsoft.Json;

namespace GDM2026.Models;

/// <summary>
/// Informations de fid�lit� d'un client r�cup�r�es apr�s scan du QR code
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
    /// Valeur en euros des couronnes (15 couronnes = 1.00�)
    /// </summary>
    public decimal ValeurEnEuros => Couronnes / 15m;

    /// <summary>
    /// Affichage format� des couronnes
    /// </summary>
    public string DisplayCouronnes => $"{Couronnes} couronne(s) = {ValeurEnEuros:C}";
}

/// <summary>
/// R�ponse de l'API lors de la r�cup�ration des infos fid�lit� par QR code
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
