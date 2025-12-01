using GDM2026.Models;

namespace GDM2026.Services
{
    public interface ISessionService
    {
        User CurrentUser { get; }
        string AuthToken { get; }

        bool IsAuthenticated { get; }

        Task<bool> LoadAsync();            // Charge depuis le stockage (Preferences/SecureStorage)
        Task SaveAsync(User user, string token); // Sauvegarde en stockage + mémoire
        Task ClearAsync();                 // Efface la session (logout)
    }
}

