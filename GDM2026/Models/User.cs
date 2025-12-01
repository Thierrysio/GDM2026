namespace GDM2026.Models;

public class User
{
    public int Id { get; set; }
    public string? Email { get; set; }
    public string? UserIdentifier { get; set; }
    public string? Token { get; set; }
    public List<string> Roles { get; set; } = new();
    public string? Nom { get; set; }
    public string? Prenom { get; set; }
    public string? Statut { get; set; }
}
