// -------------------- Services/SessionService.cs --------------------
using GDM2026.Models;
using Microsoft.Maui.Storage; // Preferences, SecureStorage
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        
        // Modification : IsAuthenticated vérifie aussi le token, pas seulement le user
        public bool IsAuthenticated => _currentUser != null || !string.IsNullOrWhiteSpace(_authToken);

        public async Task<bool> LoadAsync()
        {
            try
            {
                _authToken = await RetrieveTokenAsync();
                
                Debug.WriteLine($"[SESSION] Token retrieved: {!string.IsNullOrWhiteSpace(_authToken)}");
                Debug.WriteLine($"[SESSION] Token length: {_authToken?.Length ?? 0}");

                AppHttpClientFactory.SetBearerToken(_authToken);

                // User (JSON en Preferences)
                var userJson = Preferences.Get(KeyUser, null);
                _currentUser = string.IsNullOrWhiteSpace(userJson) ? null : JsonConvert.DeserializeObject<User>(userJson);

                Debug.WriteLine($"[SESSION] User loaded: {_currentUser != null}");
                Debug.WriteLine($"[SESSION] IsAuthenticated: {IsAuthenticated}");

                // Retourne true si on a un token OU un user
                return !string.IsNullOrWhiteSpace(_authToken) || _currentUser != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SESSION] Error loading session: {ex.Message}");
                _currentUser = null;
                _authToken = null;
                return false;
            }
        }

        public async Task SaveAsync(User user, string token)
        {
            _currentUser = SanitizeUser(user);
            _authToken = NormalizeToken(token);

            Debug.WriteLine($"[SESSION] Saving - Token: {!string.IsNullOrWhiteSpace(_authToken)}, User: {_currentUser?.Email}");

            var userJson = JsonConvert.SerializeObject(_currentUser);

            Preferences.Set(KeyUser, userJson);

            AppHttpClientFactory.SetBearerToken(_authToken);

            await StoreTokenAsync(_authToken);
        }

        public async Task ClearAsync()
        {
            Debug.WriteLine("[SESSION] Clearing session");
            _currentUser = null;
            _authToken = null;

            Preferences.Remove(KeyUser);
            SecureStorage.Remove(KeyToken);
            Preferences.Remove(KeyToken);
            AppHttpClientFactory.SetAuthorization(null);
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
                    Debug.WriteLine("[SESSION] Token found in SecureStorage");
                    return NormalizeToken(secureToken);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SESSION] SecureStorage error: {ex.Message}");
                // Fallback vers Preferences si SecureStorage indisponible
            }

            var prefToken = Preferences.Get(KeyToken, null);
            if (!string.IsNullOrWhiteSpace(prefToken))
            {
                Debug.WriteLine("[SESSION] Token found in Preferences");
            }
            else
            {
                Debug.WriteLine("[SESSION] No token found");
            }
            
            return NormalizeToken(prefToken);
        }

        private static async Task StoreTokenAsync(string token)
        {
            try
            {
                if (!string.IsNullOrEmpty(token))
                {
                    await SecureStorage.SetAsync(KeyToken, token);
                    Preferences.Remove(KeyToken);
                    Debug.WriteLine("[SESSION] Token stored in SecureStorage");
                }
                else
                {
                    SecureStorage.Remove(KeyToken);
                    Preferences.Remove(KeyToken);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SESSION] SecureStorage store error: {ex.Message}, using Preferences");
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

        private static string NormalizeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            token = token.Trim();
            const string bearerPrefix = "Bearer ";

            if (token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                token = token.Substring(bearerPrefix.Length).Trim();
            }

            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
    }
}
