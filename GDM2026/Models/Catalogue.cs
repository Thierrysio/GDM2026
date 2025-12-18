namespace GDM2026.Models;

public class Catalogue
{
    public int Id { get; set; }

    public string? Mois { get; set; }

    public int Annee { get; set; }

    public string? Url { get; set; }

    public string DisplayPeriod => string.IsNullOrWhiteSpace(Mois)
        ? Annee.ToString()
        : $"{Mois} {Annee}".Trim();

    public string DisplayUrl => string.IsNullOrWhiteSpace(Url) ? "—" : Url;
}
