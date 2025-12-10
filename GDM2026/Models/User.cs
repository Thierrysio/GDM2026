using System;
using System.Collections.Generic;
using System.Linq;

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

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Prenom) || !string.IsNullOrWhiteSpace(Nom))
            {
                return $"{Prenom} {Nom}".Trim();
            }

            if (!string.IsNullOrWhiteSpace(UserIdentifier))
            {
                return UserIdentifier!;
            }

            return Email ?? $"Utilisateur #{Id}";
        }
    }

    public string Initials
    {
        get
        {
            var initials = string.Concat(new[] { Prenom, Nom }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim()[0]))
                .ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(initials))
            {
                return initials.Length > 2 ? initials[..2] : initials;
            }

            var fallback = Email?.FirstOrDefault() ?? UserIdentifier?.FirstOrDefault() ?? 'U';
            return char.ToUpperInvariant(fallback).ToString();
        }
    }

    public string EmailLabel => string.IsNullOrWhiteSpace(Email) ? "Email non renseigné" : Email!;

    public string RolesSummary => Roles is { Count: > 0 }
        ? string.Join(", ", Roles.Select(NormalizeRole))
        : "Aucun rôle";

    public string StatusLabel => string.IsNullOrWhiteSpace(Statut) ? "Statut inconnu" : Statut!;

    private static string NormalizeRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return "?";
        }

        var cleaned = role.StartsWith("ROLE_", StringComparison.OrdinalIgnoreCase)
            ? role[5..]
            : role;

        cleaned = cleaned.Replace('_', ' ').Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return role;
        }

        return char.ToUpperInvariant(cleaned[0]) + cleaned.Substring(1).ToLowerInvariant();
    }
}
