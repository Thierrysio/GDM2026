// -------------------- Services/SessionService.cs --------------------
using GDM2026.Models;
using Microsoft.Maui.Storage; // Preferences, SecureStorage
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace GDM2026.Services
{
    public class SessionService : ISessionService
    {
        private const string KeyUser = "session.user";
        private const string KeyToken = "session.token";

        private User _currentUser;
        private string _authToken;

        public User CurrentUser => _currentUser;
        public string AuthToken => _authToken;
        public bool IsAuthenticated => _currentUser != null;

        public async Task<bool> LoadAsync()
        {
            try
            {
                _authToken = await RetrieveTokenAsync();

                // User (JSON en Preferences)
                var userJson = Preferences.Get(KeyUser, null);
                _currentUser = string.IsNullOrWhiteSpace(userJson) ? null : JsonConvert.DeserializeObject<User>(userJson);

                return _currentUser != null;
            }
            catch
            {
                _currentUser = null;
                _authToken = null;
                return false;
            }
        }

        public async Task SaveAsync(User user, string token)
        {
            _currentUser = SanitizeUser(user);
            _authToken = token ?? string.Empty;

            var userJson = JsonConvert.SerializeObject(_currentUser);

            Preferences.Set(KeyUser, userJson);

            await StoreTokenAsync(token);
        }

        public async Task ClearAsync()
        {
            _currentUser = null;
            _authToken = null;

            Preferences.Remove(KeyUser);
            SecureStorage.Remove(KeyToken);
            Preferences.Remove(KeyToken);
            await Task.CompletedTask;
        }

        private static User SanitizeUser(User user)
        {
            if (user == null) return null;

            return new User
            {
                Id = user.Id,
                Email = user.Email,
                UserIdentifier = user.UserIdentifier,
                Roles = user.Roles?.ToList() ?? new List<string>(),
                Nom = user.Nom,
                Prenom = user.Prenom,
                Statut = user.Statut
            };
        }

        private static async Task<string> RetrieveTokenAsync()
        {
            try
            {
                var secureToken = await SecureStorage.GetAsync(KeyToken);
                if (!string.IsNullOrWhiteSpace(secureToken))
                {
                    return secureToken;
                }
            }
            catch
            {
                // Fallback vers Preferences si SecureStorage indisponible
            }

            return Preferences.Get(KeyToken, null);
        }

        private static async Task StoreTokenAsync(string token)
        {
            try
            {
                if (!string.IsNullOrEmpty(token))
                {
                    await SecureStorage.SetAsync(KeyToken, token);
                    Preferences.Remove(KeyToken);
                }
                else
                {
                    SecureStorage.Remove(KeyToken);
                    Preferences.Remove(KeyToken);
                }
            }
            catch
            {
                if (!string.IsNullOrEmpty(token))
                {
                    Preferences.Set(KeyToken, token);
                }
                else
                {
                    Preferences.Remove(KeyToken);
                }
            }
        }
    }
}
