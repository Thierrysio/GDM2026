using GDM2026;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GDM2026.Services
{
    public interface IApis : IDisposable
    {
        Task<List<T>> GetListAsync<T>(string relativeUrl, CancellationToken ct = default);
        Task<TOut> GetAsync<TOut>(string relativeUrl, CancellationToken ct = default);
        Task<TResponse> PostAsync<TRequest, TResponse>(string relativeUrl, TRequest body, CancellationToken ct = default);
        Task<bool> PostBoolAsync<TRequest>(string relativeUrl, TRequest body, CancellationToken ct = default);
        Task<bool> PutBoolAsync<TRequest>(string relativeUrl, TRequest body, CancellationToken ct = default);

        void SetBearerToken(string token);
    }

    public class Apis : IApis
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerSettings _json;
        private readonly Uri? _configuredBaseUri;
        private readonly bool _ownsClient;

        /// <summary>
        /// Événement déclenché quand un appel API retourne 401 (token expiré/invalide)
        /// </summary>
        public static event EventHandler? TokenExpired;

        public HttpClient HttpClient => _http;

        public Apis(HttpClient httpClient = null)
        {
            _ownsClient = httpClient != null;
            _http = httpClient ?? AppHttpClientFactory.Create();

            if (_http.BaseAddress == null)
                _http.BaseAddress = AppHttpClientFactory.GetValidatedBaseAddress();

            _configuredBaseUri = _http.BaseAddress ?? AppHttpClientFactory.GetValidatedBaseAddress();

            if (_http.Timeout == Timeout.InfiniteTimeSpan)
                _http.Timeout = TimeSpan.FromSeconds(30);

            if (!_http.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
                _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd("GDM2026/1.0 (+maui)"))
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("GDM2026/1.0");

            _json = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.DateTime,
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Converters =
                {
                    new IsoDateTimeConverter { DateTimeFormat = "yyyy-MM-dd HH:mm:ss" },
                    new FlexibleDoubleConverter()
                }
            };
        }

        public void Dispose()
        {
            if (_ownsClient)
            {
                _http?.Dispose();
            }
        }

        public void SetBearerToken(string token)
        {
            var header = string.IsNullOrWhiteSpace(token)
                ? null
                : new AuthenticationHeaderValue("Bearer", token);

            _http.DefaultRequestHeaders.Authorization = header;
            AppHttpClientFactory.SetAuthorization(header);
        }

        private class ListResponse<T>
        {
            [JsonProperty("data")]
            public List<T> Data { get; set; } = new();
        }

        // ---------- GET : renvoie List<T> ----------
        public async Task<List<T>> GetListAsync<T>(string relativeUrl, CancellationToken ct = default)
        {
            using var reqCts = LinkedCts(ct, TimeSpan.FromSeconds(30));
            using var resp = await _http.GetAsync(BuildUri(relativeUrl), reqCts.Token).ConfigureAwait(false);
            
            await HandleUnauthorizedAsync(resp, relativeUrl).ConfigureAwait(false);
            await EnsureSuccess(resp, relativeUrl).ConfigureAwait(false);

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            var trimmedJson = json.TrimStart();

            // L'API des actualités renvoie un objet { data: [...] }, tandis que
            // d'autres endpoints retournent directement le tableau. On gère les deux
            // formats pour éviter de multiplier les appels dédiés.
            if (!trimmedJson.StartsWith("["))
            {
                var wrapped = JsonConvert.DeserializeObject<ListResponse<T>>(json, _json);
                if (wrapped?.Data != null)
                {
                    return wrapped.Data;
                }
            }

            return JsonConvert.DeserializeObject<List<T>>(json, _json) ?? new List<T>();
        }

        // ---------- GET : renvoie un TOut ----------
        public async Task<TOut> GetAsync<TOut>(string relativeUrl, CancellationToken ct = default)
        {
            try
            {
                using var reqCts = LinkedCts(ct, TimeSpan.FromSeconds(30));
                using var resp = await _http.GetAsync(BuildUri(relativeUrl), reqCts.Token).ConfigureAwait(false);
                
                await HandleUnauthorizedAsync(resp, relativeUrl).ConfigureAwait(false);
                await EnsureSuccess(resp, relativeUrl).ConfigureAwait(false);

                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<TOut>(json, _json);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new HttpRequestException($"Erreur lors de l'appel GET '{relativeUrl}'.", ex);
            }
        }

        // ---------- POST : renvoie un type de réponse ----------
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string relativeUrl, TRequest body, CancellationToken ct = default)
        {
            var payload = JsonConvert.SerializeObject(body, _json);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var reqCts = LinkedCts(ct, TimeSpan.FromSeconds(30));

            using var resp = await _http.PostAsync(BuildUri(relativeUrl), content, reqCts.Token).ConfigureAwait(false);
            
            await HandleUnauthorizedAsync(resp, relativeUrl).ConfigureAwait(false);
            await EnsureSuccess(resp, relativeUrl, payload).ConfigureAwait(false);

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<TResponse>(json, _json);
        }

        // ---------- POST : bool succès/échec ----------
        public async Task<bool> PostBoolAsync<TRequest>(string relativeUrl, TRequest body, CancellationToken ct = default)
        {
            var payload = JsonConvert.SerializeObject(body, _json);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var reqCts = LinkedCts(ct, TimeSpan.FromSeconds(30));

            using var resp = await _http.PostAsync(BuildUri(relativeUrl), content, reqCts.Token).ConfigureAwait(false);
            
            await HandleUnauthorizedAsync(resp, relativeUrl).ConfigureAwait(false);
            
            if (resp.IsSuccessStatusCode) return true;

            await EnsureSuccess(resp, relativeUrl, payload).ConfigureAwait(false);
            return false;
        }

        // ---------- PUT/PATCH : bool succès/échec ----------
        public async Task<bool> PutBoolAsync<TRequest>(string relativeUrl, TRequest body, CancellationToken ct = default)
        {
            var payload = JsonConvert.SerializeObject(body, _json);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var reqCts = LinkedCts(ct, TimeSpan.FromSeconds(30));

            using var resp = await _http.PutAsync(BuildUri(relativeUrl), content, reqCts.Token).ConfigureAwait(false);
            
            await HandleUnauthorizedAsync(resp, relativeUrl).ConfigureAwait(false);
            
            if (resp.IsSuccessStatusCode) return true;

            await EnsureSuccess(resp, relativeUrl, payload).ConfigureAwait(false);
            return false; // n’est jamais atteint si EnsureSuccess lève
        }

        // ========== Helpers ==========
        
        /// <summary>
        /// Gère les réponses 401 Unauthorized (token expiré)
        /// </summary>
        private static async Task HandleUnauthorizedAsync(HttpResponseMessage response, string path)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Debug.WriteLine($"[API] 401 Unauthorized on '{path}' - Token expired or invalid");
                
                // Déclencher l'événement pour que l'app puisse réagir
                TokenExpired?.Invoke(null, EventArgs.Empty);
                
                // Lancer une exception spécifique
                throw new HttpRequestException($"Session expirée. Veuillez vous reconnecter.", null, HttpStatusCode.Unauthorized);
            }
        }
        
        private Uri BuildUri(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("relativeUrl ne peut pas être vide", nameof(path));

            path = path.Trim();

            // 1) Si on te donne déjà une URL absolue → on accepte uniquement http(s)
            //    (sinon on considère qu'on a simplement une route absolue "/..." à
            //    concaténer avec la BaseAddress).
            if (path.Contains("://") && Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            {
                if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
                    throw new InvalidOperationException(
                        $"URL d'API invalide (schéma {absolute.Scheme}) pour '{path}'.");

                return absolute;
            }

            // 2) Sinon on considère que c'est une route relative
            if (_http.BaseAddress == null && _configuredBaseUri == null)
                throw new InvalidOperationException(
                    "Aucune BaseAddress n'est configurée pour l'API.");

            var baseUri = _http.BaseAddress ?? _configuredBaseUri!;

            // On s'assure qu'il y a bien un / devant la route
            if (!path.StartsWith("/"))
                path = "/" + path;

            return new Uri(baseUri, path);
        }

        private static async Task EnsureSuccess(HttpResponseMessage response, string path, string payload = null)
        {
            if (response.IsSuccessStatusCode) return;

            string body = null;
            try { body = await response.Content.ReadAsStringAsync().ConfigureAwait(false); } catch { }

            var msg = $"API error {(int)response.StatusCode} {response.ReasonPhrase} on '{path}'. " +
                      (payload == null ? "" : $"Payload: {Trim(payload)} ") +
                      (string.IsNullOrWhiteSpace(body) ? "" : $"Body: {Trim(body)}");

            throw new HttpRequestException(msg, null, response.StatusCode);
        }

        private static string Trim(string s, int max = 600)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";

        private static CancellationTokenSource LinkedCts(CancellationToken external, TimeSpan perRequestTimeout)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(external);
            cts.CancelAfter(perRequestTimeout);
            return cts;
        }
    }
}
